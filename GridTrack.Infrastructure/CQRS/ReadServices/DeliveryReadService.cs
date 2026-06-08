using System.Globalization;
using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Domain.Deliveries;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class DeliveryReadService : IDeliveryReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly AppDbContext _context;

    public DeliveryReadService(ISqlConnectionFactory sqlConnectionFactory, AppDbContext context)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _context = context;
    }

    public async Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DeliveryId",
                               "CurrentLocation",
                               "Status",
                               "AssignedDriverId",
                               "ExpectedEta",
                               "ActualEta",
                               "DistrictId",
                               "AnomalyFlag",
                               "CreatedAt",
                               "PickedUpAt",
                               "DeliveredAt",
                               "CancelledAt",
                               "AnomalyReason"
                           FROM public."Deliveries"
                           WHERE "DeliveryId" = @Id
                           """;

        return await connection.QueryFirstOrDefaultAsync<DeliveryDto>(sql, new { Id = id });
    }

    public async Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DeliveryId",
                               "CurrentLocation",
                               "Status",
                               "AssignedDriverId",
                               "ExpectedEta",
                               "ActualEta",
                               "DistrictId",
                               "AnomalyFlag",
                               "CreatedAt",
                               "PickedUpAt",
                               "DeliveredAt",
                               "CancelledAt",
                               "AnomalyReason"
                           FROM public."Deliveries"
                           WHERE "DistrictId" = @DistrictId
                           ORDER BY "CreatedAt" DESC
                           """;

        return await connection.QueryAsync<DeliveryDto>(sql, new { DistrictId = districtId });
    }

    public async Task<Delivery?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
        => await _context.Set<Delivery>().FirstOrDefaultAsync(d => d.DeliveryId == id, ct);

    public async Task<IEnumerable<RouteWaypointDto>> GetRouteAsync(Guid deliveryId, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT "Lat", "Lng"
                           FROM delivery_routes
                           WHERE "DeliveryId" = @DeliveryId
                           ORDER BY "Sequence"
                           """;

        return await connection.QueryAsync<RouteWaypointDto>(sql, new { DeliveryId = deliveryId });
    }

    private static readonly Dictionary<string, int> StatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["created"] = 0,
        ["assigned"] = 1,
        ["pickedup"] = 2,
        ["intransit"] = 3,
        ["delivered"] = 4,
        ["cancelled"] = 5,
        ["anomalous"] = 6,
    };

    public async Task<GetDeliveriesResponse> GetAllPaginatedAsync(
        string? cursor, string? status, string? districtId,
        DateTime? from, DateTime? to, int pageSize, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        DateTime? cursorTime = null;
        if (!string.IsNullOrWhiteSpace(cursor) &&
            DateTime.TryParse(cursor, null, DateTimeStyles.RoundtripKind, out var parsed))
            cursorTime = parsed;

        var fetchSize = pageSize > 0 ? pageSize + 1 : 1;
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (cursorTime.HasValue)
        {
            where.Add("""d."CreatedAt" < @CursorTime""");
            parameters.Add("CursorTime", cursorTime.Value);
        }
        if (from.HasValue)
        {
            where.Add("""d."CreatedAt" >= @From""");
            parameters.Add("From", from.Value);
        }
        if (to.HasValue)
        {
            where.Add("""d."CreatedAt" <= @To""");
            parameters.Add("To", to.Value);
        }
        if (!string.IsNullOrWhiteSpace(districtId))
        {
            where.Add("""d."DistrictId" = @DistrictId""");
            parameters.Add("DistrictId", districtId);
        }
        if (!string.IsNullOrWhiteSpace(status) && StatusMap.TryGetValue(status, out var statusInt))
        {
            where.Add("""d."Status" = @StatusInt""");
            parameters.Add("StatusInt", statusInt);
        }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        parameters.Add("FetchSize", fetchSize);

        var sql = $"""
                   SELECT
                       d."DeliveryId"                                                     AS "Id",
                       CASE d."Status"
                           WHEN 0 THEN 'Created'
                           WHEN 1 THEN 'Assigned'
                           WHEN 2 THEN 'PickedUp'
                           WHEN 3 THEN 'InTransit'
                           WHEN 4 THEN 'Delivered'
                           WHEN 5 THEN 'Cancelled'
                           WHEN 6 THEN 'Anomalous'
                           ELSE 'Unknown'
                       END                                                                AS "Status",
                       d."DistrictId",
                       d."AssignedDriverId",
                       dr."Name"                                                          AS "AssignedDriverName",
                       CASE WHEN d."ExpectedEta" IS NOT NULL
                            THEN EXTRACT(EPOCH FROM (d."ExpectedEta" - NOW()))::int
                            ELSE NULL END                                                 AS "EtaSeconds",
                       d."CreatedAt"
                   FROM public."Deliveries" d
                   LEFT JOIN public."Drivers" dr ON dr."DriverId" = d."AssignedDriverId"
                   {whereClause}
                   ORDER BY d."CreatedAt" DESC, d."DeliveryId" DESC
                   LIMIT @FetchSize
                   """;

        var rows = (await connection.QueryAsync<DeliveryListItemResponse>(sql, parameters)).ToList();

        string? nextCursor = null;
        if (pageSize > 0 && rows.Count > pageSize)
        {
            rows.RemoveAt(rows.Count - 1);
            nextCursor = rows.Last().CreatedAt.ToString("O");
        }

        return new GetDeliveriesResponse(rows, nextCursor, null);
    }
}
