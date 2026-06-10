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

    public async Task<GetAnalyticsSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               (SELECT COUNT(*)::int
                                FROM public."Deliveries"
                                WHERE DATE("CreatedAt" AT TIME ZONE 'UTC') = CURRENT_DATE) AS "TotalDeliveriesToday",

                               (SELECT CASE WHEN COUNT(*) = 0 THEN 0.0
                                            ELSE COUNT(*) FILTER (WHERE "Status" = 4)::float / COUNT(*)::float
                                       END
                                FROM public."Deliveries"
                                WHERE DATE("CreatedAt" AT TIME ZONE 'UTC') = CURRENT_DATE) AS "CompletionRate",

                               (SELECT COUNT(*)::int
                                FROM public."Drivers"
                                WHERE "IsActive" = true) AS "ActiveDrivers",

                               (SELECT CASE WHEN COUNT(*) = 0 THEN 0.0
                                            ELSE COUNT(*) FILTER (WHERE "AnomalyFlag" = true)::float / COUNT(*)::float
                                       END
                                FROM public."Deliveries") AS "AnomalyRate",

                               NOW() AS "UpdatedAt"
                           """;

        var row = await connection.QueryFirstAsync<GetAnalyticsSummaryResponse>(sql);
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

        var param = new { From = from, To = to };

        var deliveries = (await connection.QueryAsync<TrendPointResponse>(deliverySql, param)).ToList();
        var anomalies  = (await connection.QueryAsync<TrendPointResponse>(anomalySql,  param)).ToList();

        return new GetTrendsResponse(deliveries, anomalies);
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
}
