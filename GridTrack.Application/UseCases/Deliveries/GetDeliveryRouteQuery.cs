using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record GetDeliveryRouteQuery(Guid DeliveryId);

public sealed class GetDeliveryRouteHandler
{
    public Task<IEnumerable<RouteWaypointDto>> Handle(
        GetDeliveryRouteQuery query,
        IDeliveryReadService readService,
        CancellationToken ct)
        => readService.GetRouteAsync(query.DeliveryId, ct);
}
