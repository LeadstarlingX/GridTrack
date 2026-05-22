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
}
