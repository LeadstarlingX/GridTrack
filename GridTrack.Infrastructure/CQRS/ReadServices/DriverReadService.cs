using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Domain.Drivers;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class DriverReadService : IDriverReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly AppDbContext _context;

    public DriverReadService(ISqlConnectionFactory sqlConnectionFactory, AppDbContext context)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _context = context;
    }

    public async Task<IEnumerable<DriverDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DriverId",
                               "Location",
                               "IsActive",
                               "LastSeen",
                               "DistrictId"
                           FROM public."Drivers"
                           WHERE "DistrictId" = @DistrictId
                           ORDER BY "LastSeen" DESC
                           """;

        return await connection.QueryAsync<DriverDto>(sql, new { DistrictId = districtId });
    }

    public async Task<IEnumerable<DriverDto>> GetNearestAsync(Point location, int count, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DriverId",
                               "Location",
                               "IsActive",
                               "LastSeen",
                               "DistrictId"
                           FROM public."Drivers"
                           WHERE "IsActive" = true
                           ORDER BY "Location" <-> ST_GeogFromText(@LocationWkt)
                           LIMIT @Count
                           """;

        var locationWkt = $"POINT({location.X} {location.Y})";
        return await connection.QueryAsync<DriverDto>(sql, new { LocationWkt = locationWkt, Count = count });
    }

    public async Task<Driver?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
        => await _context.Set<Driver>().FirstOrDefaultAsync(d => d.DriverId == id, ct);

    public async Task<GetDriversResponse> GetAllAsync(
        string? cursor, string? districtId, string? status, int pageSize, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        // Derive status from deliveries:
        //   IsActive = false              → "offline"
        //   has anomalous delivery        → "stalled"
        //   has in-transit delivery       → "in-transit"
        //   otherwise                     → "available"
        const string sql = """
            SELECT
                d."DriverId"                                       AS "Id",
                d."Name",
                d."ShortName",
                ST_Y(d."Location")                                 AS "Lat",
                ST_X(d."Location")                                 AS "Lng",
                d."DistrictId",
                d."DistrictId"                                     AS "DistrictName",
                CASE
                    WHEN d."IsActive" = false                                  THEN 'offline'
                    WHEN COUNT(del."DeliveryId") FILTER (WHERE del."AnomalyFlag" = true AND del."Status" NOT IN (4,5)) > 0
                                                                               THEN 'stalled'
                    WHEN COUNT(del."DeliveryId") FILTER (WHERE del."Status" = 3) > 0
                                                                               THEN 'in-transit'
                    ELSE 'available'
                END AS "Status",
                COUNT(del."DeliveryId") FILTER (WHERE del."Status" NOT IN (4,5)) AS "ActiveDeliveries",
                COUNT(del."DeliveryId") FILTER (
                    WHERE del."Status" = 4
                    AND del."DeliveredAt" >= CURRENT_DATE
                    AND del."DeliveredAt" < CURRENT_DATE + INTERVAL '1 day'
                ) AS "CompletedToday",
                BOOL_OR(del."AnomalyFlag" AND del."Status" NOT IN (4,5)) AS "HasAnomaly",
                MAX(CASE WHEN del."AnomalyFlag" = true AND del."Status" NOT IN (4,5) THEN del."AnomalyReason" END) AS "AnomalyReason",
                d."CarType"     AS "CarType",
                d."LicensePlate" AS "LicensePlate",
                d."PhoneNumber" AS "PhoneNumber"
            FROM "Drivers" d
            LEFT JOIN "Deliveries" del ON del."AssignedDriverId" = d."DriverId"
            WHERE (@DistrictId IS NULL OR d."DistrictId" = @DistrictId)
              AND (@Cursor IS NULL OR d."DriverId"::text > @Cursor)
            GROUP BY d."DriverId", d."Name", d."ShortName", d."Location", d."DistrictId", d."IsActive", d."CarType", d."LicensePlate", d."PhoneNumber"
            HAVING (@Status IS NULL
                OR (
                    @Status = 'offline'    AND d."IsActive" = false
                ) OR (
                    @Status = 'stalled'    AND d."IsActive" = true
                    AND BOOL_OR(del."AnomalyFlag" AND del."Status" NOT IN (4,5))
                ) OR (
                    @Status = 'in-transit' AND d."IsActive" = true
                    AND COUNT(del."DeliveryId") FILTER (WHERE del."Status" = 3) > 0
                ) OR (
                    @Status = 'available'  AND d."IsActive" = true
                    AND COUNT(del."DeliveryId") FILTER (WHERE del."Status" NOT IN (4,5) AND del."AnomalyFlag" = true) = 0
                    AND COUNT(del."DeliveryId") FILTER (WHERE del."Status" = 3) = 0
                )
            )
            ORDER BY d."DriverId"
            LIMIT @PageSize
            """;

        var rows = (await connection.QueryAsync<DriverListItemResponse>(
            sql,
            new { Cursor = cursor, DistrictId = districtId, Status = status, PageSize = pageSize + 1 }))
            .ToList();

        string? nextCursor = null;
        if (rows.Count > pageSize)
        {
            rows = rows.Take(pageSize).ToList();
            nextCursor = rows[^1].Id.ToString();
        }

        return new GetDriversResponse(rows, nextCursor, null);
    }
}
