using GridTrack.Application.Dtos;
using GridTrack.Domain.Deliveries;


namespace GridTrack.Application.CQRS.ReadServices;

public interface IDeliveryReadService
{
    Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct);
    Task<Delivery?> GetAggregateByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<RouteWaypointDto>> GetRouteAsync(Guid deliveryId, CancellationToken ct);
    Task<GetDeliveriesResponse> GetAllPaginatedAsync(
        string? cursor, string? status, string? districtId,
        DateTime? from, DateTime? to, int pageSize, CancellationToken ct);
}
