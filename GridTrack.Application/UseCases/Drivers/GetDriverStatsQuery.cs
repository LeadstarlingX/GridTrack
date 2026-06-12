using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Dtos;
using Dapper;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record GetDriverStatsQuery(Guid DriverId);

public sealed class GetDriverStatsHandler
{
    public async Task<DriverStatsResponse?> Handle(
        GetDriverStatsQuery query,
        ISqlConnectionFactory sqlFactory,
        CancellationToken ct)
    {
        using var conn = sqlFactory.CreateConnection();

        const string sql = """
            SELECT
                d."DriverId",
                d."Name",
                COUNT(del."DeliveryId") FILTER (WHERE del."Status" = 4)::int                                AS "TotalCompleted",
                COUNT(del."DeliveryId") FILTER (
                    WHERE del."Status" = 4
                      AND del."DeliveredAt" >= CURRENT_DATE
                      AND del."DeliveredAt" <  CURRENT_DATE + INTERVAL '1 day'
                )::int                                                                                      AS "CompletedToday",
                COUNT(del."DeliveryId") FILTER (WHERE del."Status" = 5)::int                                AS "TotalCancelled",
                COUNT(del."DeliveryId") FILTER (WHERE del."Status" NOT IN (4, 5))::int                      AS "ActiveDeliveries",
                COALESCE(
                    100.0 * COUNT(del."DeliveryId") FILTER (
                        WHERE del."Status" = 4
                          AND del."ExpectedEta" IS NOT NULL
                          AND del."DeliveredAt" <= del."ExpectedEta"
                    )::float
                    / NULLIF(COUNT(del."DeliveryId") FILTER (
                        WHERE del."Status" = 4
                          AND del."ExpectedEta" IS NOT NULL
                    ), 0),
                0)                                                                                          AS "OnTimeRatePct"
            FROM public."Drivers" d
            LEFT JOIN public."Deliveries" del ON del."AssignedDriverId" = d."DriverId"
            WHERE d."DriverId" = @DriverId
            GROUP BY d."DriverId", d."Name"
            """;

        return await conn.QueryFirstOrDefaultAsync<DriverStatsResponse>(sql, new { query.DriverId });
    }
}
