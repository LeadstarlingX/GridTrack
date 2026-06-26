using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record GetDeliveryByIdQuery(Guid DeliveryId);

public sealed class GetDeliveryByIdHandler
{
    public async Task<GetDeliveryByIdResponse?> Handle(
        GetDeliveryByIdQuery query,
        IDeliveryReadService readService,
        CancellationToken ct)
    {
        var delivery = await readService.GetByIdAsync(query.DeliveryId, ct);
        if (delivery is null)
            return null;

        var updatedAt = delivery.DeliveredAt ?? delivery.PickedUpAt ?? delivery.CancelledAt;

        var waypoints = await readService.GetRouteAsync(query.DeliveryId, ct);
        var polyline = waypoints
            .Select(w => new CoordinateResponse(w.Lat, w.Lng))
            .ToArray();

        int? etaSeconds = delivery.ExpectedEta.HasValue
            ? (int)Math.Max(0, (delivery.ExpectedEta.Value - DateTime.UtcNow).TotalSeconds)
            : null;

        return new GetDeliveryByIdResponse(
            delivery.DeliveryId,
            delivery.Status.ToString(),
            delivery.DistrictId,
            delivery.AssignedDriverId,
            AssignedDriverName: null,
            EtaSeconds: etaSeconds,
            delivery.CreatedAt,
            updatedAt,
            RoutePolyline: polyline,
            delivery.RouteDistanceMeters,
            delivery.RouteDurationSeconds,
            delivery.RouteCost,
            CurrentLat: delivery.CurrentLocation?.Coordinate?.Y,
            CurrentLng: delivery.CurrentLocation?.Coordinate?.X);
    }
}