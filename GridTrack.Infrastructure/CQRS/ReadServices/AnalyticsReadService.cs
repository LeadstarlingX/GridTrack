using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class AnalyticsReadService : IAnalyticsReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public AnalyticsReadService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<GetAnalyticsSummaryResponse> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        // When no range is given fall back to today (UTC). The "to" bound is exclusive end-of-day.
        var rangeFrom = from ?? DateTime.UtcNow.Date;
        var rangeTo   = (to ?? DateTime.UtcNow.Date).Date.AddDays(1);

        const string sql = """
                           SELECT
                               (SELECT COUNT(*)::int
                                FROM public."Deliveries"
                                WHERE "CreatedAt" >= @From AND "CreatedAt" < @To) AS "TotalDeliveriesToday",

                               (SELECT CASE WHEN COUNT(*) = 0 THEN 0.0
                                            ELSE COUNT(*) FILTER (WHERE "Status" = 4)::float / COUNT(*)::float
                                       END
                                FROM public."Deliveries"
                                WHERE "CreatedAt" >= @From AND "CreatedAt" < @To) AS "CompletionRate",

                               (SELECT COUNT(*)::int
                                FROM public."Drivers"
                                WHERE "IsActive" = true) AS "ActiveDrivers",

                               (SELECT CASE WHEN COUNT(*) = 0 THEN 0.0
                                            ELSE COUNT(*) FILTER (WHERE "AnomalyFlag" = true)::float / COUNT(*)::float
                                       END
                                FROM public."Deliveries"
                                WHERE "CreatedAt" >= @From AND "CreatedAt" < @To) AS "AnomalyRate",

                               (SELECT COUNT(*)::int
                                FROM public."Deliveries"
                                WHERE "Status" = 0) AS "PendingDeliveries",

                               (SELECT COALESCE(
                                    AVG(EXTRACT(EPOCH FROM ("DeliveredAt" - "PickedUpAt")) / 60.0), 0)::float
                                FROM public."Deliveries"
                                WHERE "Status" = 4
                                  AND "DeliveredAt" IS NOT NULL
                                  AND "PickedUpAt" IS NOT NULL
                                  AND "DeliveredAt" >= @From AND "DeliveredAt" < @To) AS "AvgDeliveryMinutes",

                               (SELECT CASE
                                    WHEN COUNT(*) FILTER (WHERE "ExpectedEta" IS NOT NULL) = 0 THEN 0.0
                                    ELSE COUNT(*) FILTER (WHERE "ExpectedEta" IS NOT NULL AND "DeliveredAt" <= "ExpectedEta")::float
                                         / COUNT(*) FILTER (WHERE "ExpectedEta" IS NOT NULL)::float * 100.0
                                    END
                                FROM public."Deliveries"
                                WHERE "Status" = 4
                                  AND "DeliveredAt" >= @From AND "DeliveredAt" < @To) AS "OnTimeRatePct",

                               NOW() AS "UpdatedAt"
                           """;

        var row = await connection.QueryFirstAsync<GetAnalyticsSummaryResponse>(sql, new { From = rangeFrom, To = rangeTo });
        return row;
    }

    public async Task<GetH3DensityResponse> GetH3DensityAsync(
        DateTime from,
        DateTime to,
        int resolution,
        int? fromHour,
        int? toHour,
        CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DistrictId"                                    AS "H3Index",
                               AVG(ST_Y("CurrentLocation"::geometry))::float   AS "Lat",
                               AVG(ST_X("CurrentLocation"::geometry))::float   AS "Lng",
                               COUNT(*)::int                                    AS "DeliveryCount"
                           FROM public."Deliveries"
                           WHERE "CreatedAt" >= @From
                             AND "CreatedAt" <= @To
                             AND (@FromHour IS NULL OR EXTRACT(HOUR FROM "CreatedAt" AT TIME ZONE 'UTC') >= @FromHour)
                             AND (@ToHour   IS NULL OR EXTRACT(HOUR FROM "CreatedAt" AT TIME ZONE 'UTC') <= @ToHour)
                           GROUP BY "DistrictId"
                           ORDER BY "DeliveryCount" DESC
                           """;

        var cells = await connection.QueryAsync<H3DensityCellResponse>(sql, new
        {
            From = from,
            To = to,
            FromHour = fromHour,
            ToHour = toHour,
        });

        return new GetH3DensityResponse(cells.ToList());
    }

    public async Task<GetTrendsResponse> GetTrendsAsync(
        DateTime from,
        DateTime to,
        string granularity,
        CancellationToken ct)
    {
        // Whitelist granularity to prevent SQL injection
        var safeTrunc = granularity.ToLower() switch
        {
            "hour"  => "hour",
            "week"  => "week",
            "month" => "month",
            _       => "day",
        };

        using var connection = _sqlConnectionFactory.CreateConnection();

        var deliverySql = $"""
                           SELECT
                               date_trunc('{safeTrunc}', "CreatedAt" AT TIME ZONE 'UTC')::text AS "Bucket",
                               COUNT(*)::float                                                   AS "Value"
                           FROM public."Deliveries"
                           WHERE "CreatedAt" >= @From AND "CreatedAt" <= @To
                           GROUP BY 1
                           ORDER BY 1
                           """;

        var anomalySql = $"""
                          SELECT
                              date_trunc('{safeTrunc}', "CreatedAt" AT TIME ZONE 'UTC')::text AS "Bucket",
                              COUNT(*)::float                                                   AS "Value"
                          FROM public."Deliveries"
                          WHERE "CreatedAt" >= @From AND "CreatedAt" <= @To
                            AND "AnomalyFlag" = true
                          GROUP BY 1
                          ORDER BY 1
                          """;

        var urgencySql = $"""
                          SELECT
                              date_trunc('{safeTrunc}', "UrgencyScoreAt" AT TIME ZONE 'UTC')::text AS "Bucket",
                              AVG("UrgencyScore")::float                                            AS "Value"
                          FROM public."Deliveries"
                          WHERE "UrgencyScore" IS NOT NULL
                            AND "UrgencyScoreAt" IS NOT NULL
                            AND "UrgencyScoreAt" >= @From AND "UrgencyScoreAt" <= @To
                          GROUP BY 1
                          ORDER BY 1
                          """;

        var param = new { From = from, To = to };

        var deliveries = (await connection.QueryAsync<TrendPointResponse>(deliverySql, param)).ToList();
        var anomalies  = (await connection.QueryAsync<TrendPointResponse>(anomalySql,  param)).ToList();
        var urgency    = (await connection.QueryAsync<TrendPointResponse>(urgencySql,   param)).ToList();

        return new GetTrendsResponse(deliveries, anomalies, urgency);
    }

    public async Task<GetDistrictVolumeResponse> GetDistrictVolumeAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DistrictId"   AS "DistrictId",
                               COUNT(*)::int  AS "Deliveries"
                           FROM public."Deliveries"
                           WHERE (@From::timestamptz IS NULL OR "CreatedAt" >= @From::timestamptz)
                             AND (@To::timestamptz   IS NULL OR "CreatedAt" <= @To::timestamptz)
                           GROUP BY "DistrictId"
                           ORDER BY "Deliveries" DESC
                           """;

        var items = await connection.QueryAsync<DistrictVolumeItemResponse>(sql, new { From = from, To = to });
        return new GetDistrictVolumeResponse(items.ToList());
    }

    public async Task<GetCancellationAnalyticsResponse> GetCancellationAnalyticsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        // Status 5 = Cancelled. A late cancellation is one flagged anomalous on cancel
        // (see Delivery.MarkCancelled — cancelled at/after ETA sets AnomalyFlag).
        const string countsSql = """
                           SELECT
                               COUNT(*) FILTER (WHERE "Status" = 5)::int AS "TotalCancelled",
                               COUNT(*) FILTER (WHERE "Status" = 5 AND "AnomalyFlag" = true)::int AS "LateCancellations",
                               CASE WHEN COUNT(*) = 0 THEN 0.0
                                    ELSE COUNT(*) FILTER (WHERE "Status" = 5)::float / COUNT(*)::float
                               END AS "CancellationRate"
                           FROM public."Deliveries"
                           WHERE (@From::timestamptz IS NULL OR "CreatedAt" >= @From::timestamptz)
                             AND (@To::timestamptz   IS NULL OR "CreatedAt" <= @To::timestamptz)
                           """;

        const string reasonsSql = """
                           SELECT
                               COALESCE(NULLIF("AnomalyReason", ''), '(none)') AS "Reason",
                               COUNT(*)::int                                    AS "Count"
                           FROM public."Deliveries"
                           WHERE "Status" = 5
                             AND (@From::timestamptz IS NULL OR "CreatedAt" >= @From::timestamptz)
                             AND (@To::timestamptz   IS NULL OR "CreatedAt" <= @To::timestamptz)
                           GROUP BY 1
                           ORDER BY "Count" DESC
                           """;

        var param = new { From = from, To = to };
        var counts = await connection.QueryFirstAsync<CancellationCounts>(countsSql, param);
        var reasons = await connection.QueryAsync<CancellationReasonItemResponse>(reasonsSql, param);

        return new GetCancellationAnalyticsResponse(
            counts.TotalCancelled,
            counts.LateCancellations,
            counts.CancellationRate,
            reasons.ToList());
    }

    private sealed record CancellationCounts(int TotalCancelled, int LateCancellations, double CancellationRate);

    public async Task<GetDeliveryPerformanceResponse> GetDeliveryPerformanceAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        // Status 4 = Delivered. Actual duration = DeliveredAt − PickedUpAt;
        // expected duration = ExpectedEta − CreatedAt; on-time = DeliveredAt <= ExpectedEta.
        const string sql = """
                           SELECT
                               "DistrictId"                                                                          AS "DistrictId",
                               COUNT(*)::int                                                                         AS "DeliveredCount",
                               COALESCE(AVG(EXTRACT(EPOCH FROM ("DeliveredAt" - "PickedUpAt"))), 0)::float           AS "AvgActualDurationSeconds",
                               COALESCE(AVG(EXTRACT(EPOCH FROM ("ExpectedEta" - "CreatedAt")))
                                        FILTER (WHERE "ExpectedEta" IS NOT NULL), 0)::float                          AS "AvgExpectedDurationSeconds",
                               COUNT(*) FILTER (WHERE "ExpectedEta" IS NOT NULL AND "DeliveredAt" <= "ExpectedEta")::int AS "OnTimeCount"
                           FROM public."Deliveries"
                           WHERE "Status" = 4 AND "DeliveredAt" IS NOT NULL AND "PickedUpAt" IS NOT NULL
                             AND (@From::timestamptz IS NULL OR "CreatedAt" >= @From::timestamptz)
                             AND (@To::timestamptz   IS NULL OR "CreatedAt" <= @To::timestamptz)
                           GROUP BY "DistrictId"
                           ORDER BY "DeliveredCount" DESC
                           """;

        var rows = (await connection.QueryAsync<PerformanceRow>(sql, new { From = from, To = to })).ToList();

        var districts = rows
            .Select(r => new DistrictPerformanceItemResponse(
                r.DistrictId,
                r.DeliveredCount,
                r.AvgActualDurationSeconds,
                r.AvgExpectedDurationSeconds,
                r.DeliveredCount == 0 ? 0 : (double)r.OnTimeCount / r.DeliveredCount))
            .ToList();

        var totalDelivered = rows.Sum(r => r.DeliveredCount);
        var overallOnTime = totalDelivered == 0 ? 0 : (double)rows.Sum(r => r.OnTimeCount) / totalDelivered;
        var overallAvgDuration = totalDelivered == 0
            ? 0
            : rows.Sum(r => r.AvgActualDurationSeconds * r.DeliveredCount) / totalDelivered;

        return new GetDeliveryPerformanceResponse(totalDelivered, overallOnTime, overallAvgDuration, districts);
    }

    public async Task<GetDriverUtilizationResponse> GetDriverUtilizationAsync(int topCount, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        // Active delivery = Status NOT IN (4 Delivered, 5 Cancelled); completed today = Status 4 delivered today.
        const string sql = """
                           SELECT
                               d."DriverId"  AS "DriverId",
                               d."Name"      AS "Name",
                               d."IsActive"  AS "IsActive",
                               COUNT(del."DeliveryId") FILTER (
                                   WHERE del."Status" = 4
                                     AND del."DeliveredAt" >= CURRENT_DATE
                                     AND del."DeliveredAt" <  CURRENT_DATE + INTERVAL '1 day'
                               )::int AS "CompletedToday",
                               COUNT(del."DeliveryId") FILTER (WHERE del."Status" NOT IN (4, 5))::int AS "ActiveDeliveries"
                           FROM public."Drivers" d
                           LEFT JOIN public."Deliveries" del ON del."AssignedDriverId" = d."DriverId"
                           GROUP BY d."DriverId", d."Name", d."IsActive"
                           ORDER BY "CompletedToday" DESC, "ActiveDeliveries" DESC
                           """;

        var rows = (await connection.QueryAsync<DriverThroughputRow>(sql)).ToList();

        var activeDrivers = rows.Count(r => r.IsActive);
        var inactiveDrivers = rows.Count(r => !r.IsActive);
        var avgActive = activeDrivers == 0
            ? 0
            : (double)rows.Where(r => r.IsActive).Sum(r => r.ActiveDeliveries) / activeDrivers;

        var top = rows
            .Take(topCount)
            .Select(r => new DriverThroughputItemResponse(r.DriverId, r.Name, r.CompletedToday, r.ActiveDeliveries))
            .ToList();

        return new GetDriverUtilizationResponse(activeDrivers, inactiveDrivers, avgActive, top);
    }

    public async Task<GetAnomalyBreakdownResponse> GetAnomalyBreakdownAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string byTypeSql = """
                           SELECT
                               "AnomalyTypeValue" AS "AnomalyType",
                               COUNT(*)::int      AS "Count"
                           FROM public."Deliveries"
                           WHERE "AnomalyFlag" = true AND "AnomalyTypeValue" IS NOT NULL
                             AND (@From::timestamptz IS NULL OR "CreatedAt" >= @From::timestamptz)
                             AND (@To::timestamptz   IS NULL OR "CreatedAt" <= @To::timestamptz)
                           GROUP BY "AnomalyTypeValue"
                           ORDER BY "Count" DESC
                           """;

        const string byDistrictSql = """
                           SELECT
                               "DistrictId"  AS "DistrictId",
                               COUNT(*)::int AS "Count"
                           FROM public."Deliveries"
                           WHERE "AnomalyFlag" = true
                             AND (@From::timestamptz IS NULL OR "CreatedAt" >= @From::timestamptz)
                             AND (@To::timestamptz   IS NULL OR "CreatedAt" <= @To::timestamptz)
                           GROUP BY "DistrictId"
                           ORDER BY "Count" DESC
                           """;

        var param = new { From = from, To = to };
        var byType = (await connection.QueryAsync<AnomalyTypeCountResponse>(byTypeSql, param)).ToList();
        var byDistrict = (await connection.QueryAsync<AnomalyDistrictCountResponse>(byDistrictSql, param)).ToList();

        return new GetAnomalyBreakdownResponse(byType, byDistrict);
    }

    public async Task<GetDriverAnalyticsResponse> GetDriverAnalyticsAsync(CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        // ── Per-driver aggregate stats (7-day rolling window) ──────────────
        const string mainSql = """
            SELECT
                d."DriverId",
                d."Name",
                d."CarType",
                d."DistrictId",
                COUNT(del."DeliveryId") FILTER (
                    WHERE del."CreatedAt" >= NOW() - INTERVAL '7 days'
                )::int AS "TotalLast7Days",
                COUNT(del."DeliveryId") FILTER (
                    WHERE del."Status" = 4
                      AND del."DeliveredAt" >= NOW() - INTERVAL '7 days'
                )::int AS "CompletedLast7Days",
                CASE
                    WHEN COUNT(del."DeliveryId") FILTER (
                        WHERE del."Status" = 4
                          AND del."ExpectedEta" IS NOT NULL
                          AND del."DeliveredAt" >= NOW() - INTERVAL '7 days'
                    ) = 0 THEN NULL
                    ELSE COUNT(del."DeliveryId") FILTER (
                             WHERE del."Status" = 4
                               AND del."ExpectedEta" IS NOT NULL
                               AND del."DeliveredAt" <= del."ExpectedEta"
                               AND del."DeliveredAt" >= NOW() - INTERVAL '7 days'
                         )::float
                         / COUNT(del."DeliveryId") FILTER (
                             WHERE del."Status" = 4
                               AND del."ExpectedEta" IS NOT NULL
                               AND del."DeliveredAt" >= NOW() - INTERVAL '7 days'
                         )::float
                END AS "OnTimeRatePct",
                CASE
                    WHEN COUNT(del."DeliveryId") FILTER (
                        WHERE del."CreatedAt" >= NOW() - INTERVAL '7 days'
                    ) = 0 THEN 0.0
                    ELSE COUNT(del."DeliveryId") FILTER (
                             WHERE del."AnomalyFlag" = true
                               AND del."CreatedAt" >= NOW() - INTERVAL '7 days'
                         )::float
                         / COUNT(del."DeliveryId") FILTER (
                             WHERE del."CreatedAt" >= NOW() - INTERVAL '7 days'
                         )::float
                END AS "AnomalyRate",
                COALESCE(AVG(
                    EXTRACT(EPOCH FROM (del."DeliveredAt" - del."PickedUpAt"))
                ) FILTER (
                    WHERE del."Status" = 4
                      AND del."DeliveredAt" IS NOT NULL
                      AND del."PickedUpAt"  IS NOT NULL
                      AND del."DeliveredAt" >= NOW() - INTERVAL '7 days'
                ), 0)::float AS "AvgDurationSeconds"
            FROM "Drivers" d
            LEFT JOIN "Deliveries" del ON del."AssignedDriverId" = d."DriverId"
            GROUP BY d."DriverId", d."Name", d."CarType", d."DistrictId"
            ORDER BY "TotalLast7Days" DESC, d."Name"
            """;

        // ── District-average duration (7-day) for comparison ───────────────
        const string districtSql = """
            SELECT
                "DistrictId",
                COALESCE(AVG(EXTRACT(EPOCH FROM ("DeliveredAt" - "PickedUpAt"))), 0)::float AS "AvgDurationSeconds"
            FROM "Deliveries"
            WHERE "Status" = 4
              AND "DeliveredAt" IS NOT NULL
              AND "PickedUpAt"  IS NOT NULL
              AND "DeliveredAt" >= NOW() - INTERVAL '7 days'
            GROUP BY "DistrictId"
            """;

        // ── Hourly on-time rate per driver (7-day) ─────────────────────────
        const string hourlySql = """
            SELECT
                del."AssignedDriverId" AS "DriverId",
                EXTRACT(HOUR FROM del."DeliveredAt" AT TIME ZONE 'UTC')::int AS "Hour",
                COUNT(*) FILTER (WHERE del."DeliveredAt" <= del."ExpectedEta")::float
                    / NULLIF(COUNT(*)::float, 0) AS "OnTimeRatePct",
                COUNT(*)::int AS "SampleCount"
            FROM "Deliveries" del
            WHERE del."Status" = 4
              AND del."AssignedDriverId" IS NOT NULL
              AND del."ExpectedEta" IS NOT NULL
              AND del."DeliveredAt" IS NOT NULL
              AND del."DeliveredAt" >= NOW() - INTERVAL '7 days'
            GROUP BY del."AssignedDriverId",
                     EXTRACT(HOUR FROM del."DeliveredAt" AT TIME ZONE 'UTC')
            ORDER BY del."AssignedDriverId", "Hour"
            """;

        var mainRows    = (await connection.QueryAsync<DriverStatsRow>(mainSql)).ToList();
        var districtAvg = (await connection.QueryAsync<DistrictAvgRow>(districtSql))
                              .ToDictionary(r => r.DistrictId, r => r.AvgDurationSeconds);
        var hourlyRows  = (await connection.QueryAsync<HourlyRow>(hourlySql))
                              .GroupBy(r => r.DriverId)
                              .ToDictionary(g => g.Key, g => g.ToList());

        var drivers = mainRows
            .Select(r =>
            {
                var hourly = hourlyRows.TryGetValue(r.DriverId, out var h)
                    ? h.Select(p => new HourlyOnTimePointDto(p.Hour, p.OnTimeRatePct, p.SampleCount))
                       .ToList()
                    : [];

                return new DriverAnalyticsItemResponse(
                    r.DriverId,
                    r.Name,
                    r.CarType,
                    r.DistrictId,
                    r.TotalLast7Days,
                    r.CompletedLast7Days,
                    r.OnTimeRatePct,
                    r.AnomalyRate,
                    r.AvgDurationSeconds,
                    districtAvg.GetValueOrDefault(r.DistrictId, 0),
                    hourly);
            })
            .ToList();

        return new GetDriverAnalyticsResponse(drivers);
    }

    private sealed record DriverStatsRow(
        Guid DriverId,
        string Name,
        string? CarType,
        string DistrictId,
        int TotalLast7Days,
        int CompletedLast7Days,
        double? OnTimeRatePct,
        double AnomalyRate,
        double AvgDurationSeconds);

    private sealed record DistrictAvgRow(string DistrictId, double AvgDurationSeconds);

    private sealed record HourlyRow(Guid DriverId, int Hour, double OnTimeRatePct, int SampleCount);

    private sealed record PerformanceRow(
        string DistrictId,
        int DeliveredCount,
        double AvgActualDurationSeconds,
        double AvgExpectedDurationSeconds,
        int OnTimeCount);

    private sealed record DriverThroughputRow(
        Guid DriverId,
        string Name,
        bool IsActive,
        int CompletedToday,
        int ActiveDeliveries);
}
