using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Dispatch;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.Dispatch;

internal sealed class WeightedDispatchStrategy(
    ISqlConnectionFactory sqlConnectionFactory,
    IOptions<DispatchWeightsOptions> opts) : IDispatchStrategy
{
    // Fetch a pool larger than requested so scoring can re-rank beyond distance order.
    private const int PoolMultiplier = 3;

    public async Task<IReadOnlyList<DispatchCandidateDto>> GetCandidatesAsync(
        Point deliveryLocation, int count, CancellationToken ct)
    {
        using var connection = sqlConnectionFactory.CreateConnection();

        var locationWkt  = $"POINT({deliveryLocation.X} {deliveryLocation.Y})";
        var candidatePool = count * PoolMultiplier;

        // Pulling a broader candidate pool sorted by raw proximity lets scoring
        // promote high on-time / low-load drivers that aren't the absolute closest.
        const string sql = """
            SELECT
                d."DriverId"   AS "DriverId",
                d."Name"       AS "Name",
                d."ShortName"  AS "ShortName",
                d."DistrictId" AS "DistrictId",
                ST_Distance(d."Location"::geography, ST_GeogFromText(@LocationWkt)) AS "DistanceM",
                CASE
                    WHEN COUNT(del."DeliveryId") FILTER (
                        WHERE del."Status" = 4
                          AND del."DeliveredAt" >= NOW() - INTERVAL '7 days'
                          AND del."ExpectedEta" IS NOT NULL
                    ) = 0 THEN NULL
                    ELSE
                        CAST(COUNT(del."DeliveryId") FILTER (
                            WHERE del."Status" = 4
                              AND del."DeliveredAt" >= NOW() - INTERVAL '7 days'
                              AND del."ExpectedEta" IS NOT NULL
                              AND del."DeliveredAt" <= del."ExpectedEta"
                        ) AS double precision)
                        /
                        COUNT(del."DeliveryId") FILTER (
                            WHERE del."Status" = 4
                              AND del."DeliveredAt" >= NOW() - INTERVAL '7 days'
                              AND del."ExpectedEta" IS NOT NULL
                        )
                END AS "OnTimeRatePct",
                COUNT(del."DeliveryId") FILTER (
                    WHERE del."Status" NOT IN (4, 5)
                )::int AS "ActiveDeliveries",
                d."ShiftStartedAt" AS "ShiftStartedAt",
                d."ShiftEndsAt"    AS "ShiftEndsAt"
            FROM public."Drivers" d
            LEFT JOIN public."Deliveries" del ON del."AssignedDriverId" = d."DriverId"
            WHERE d."IsActive" = true
            GROUP BY d."DriverId", d."Name", d."ShortName", d."DistrictId",
                     d."Location", d."ShiftStartedAt", d."ShiftEndsAt"
            ORDER BY "DistanceM"
            LIMIT @CandidatePool
            """;

        var rows = await connection.QueryAsync<CandidateRow>(
            sql, new { LocationWkt = locationWkt, CandidatePool = candidatePool });

        var w   = opts.Value;
        var now = DateTime.UtcNow;

        return rows
            .Select(r => Score(r, w, now))
            .OrderByDescending(c => c.Score)
            .Take(count)
            .ToList();
    }

    private static DispatchCandidateDto Score(CandidateRow r, DispatchWeightsOptions w, DateTime now)
    {
        var proximityScore = 1.0 / (1.0 + r.DistanceM / 1000.0);   // smooth km-decay: 0m→1.0, 1km→0.5, 10km→0.09
        var onTimeScore    = r.OnTimeRatePct ?? 0.5;                 // neutral when no history
        var loadScore      = 1.0 / (1.0 + r.ActiveDeliveries);      // 0 active→1.0, 1→0.5, 2→0.33
        var shiftScore     = ComputeShiftScore(r.ShiftStartedAt, r.ShiftEndsAt, now);

        var composite = w.Proximity  * proximityScore
                      + w.OnTimeRate * onTimeScore
                      + w.LoadScore  * loadScore
                      + w.ShiftScore * shiftScore;

        return new DispatchCandidateDto(
            r.DriverId, r.Name, r.ShortName, r.DistrictId,
            Math.Round(r.DistanceM, 1),
            r.OnTimeRatePct,
            r.ActiveDeliveries,
            shiftScore,
            Math.Round(composite, 4));
    }

    private static double ComputeShiftScore(DateTime? start, DateTime? end, DateTime now)
    {
        if (start is null || end is null) return 0.5;
        return now >= start.Value && now <= end.Value ? 1.0 : 0.0;
    }

    private sealed record CandidateRow(
        Guid      DriverId,
        string    Name,
        string    ShortName,
        string    DistrictId,
        double    DistanceM,
        double?   OnTimeRatePct,
        int       ActiveDeliveries,
        DateTime? ShiftStartedAt,
        DateTime? ShiftEndsAt);
}
