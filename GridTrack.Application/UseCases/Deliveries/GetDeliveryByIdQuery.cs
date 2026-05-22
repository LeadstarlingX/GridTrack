using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;

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

        return new GetDeliveryByIdResponse(
            delivery.DeliveryId,
            delivery.Status.ToString(),
            delivery.DistrictId,
            delivery.AssignedDriverId,
            AssignedDriverName: null,
            EtaSeconds: null,
            delivery.CreatedAt,
            updatedAt,
            RoutePolyline: Array.Empty<CoordinateResponse>());
    }
}
