using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.CQRS.Handlers;

public static class DeliveryUpdateBroadcastHandler
{
    public static async Task Handle(
        DeliveryLocationUpdatedDomainEvent e,
        IDeliveryReadService readService,
        IDashboardPushService push,
        CancellationToken ct)
    {
        var delivery = await readService.GetAggregateByIdAsync(e.DeliveryId, ct);
        if (delivery is null)
            return;

        await push.BroadcastDeliveryUpdateAsync(
            delivery.DistrictId,
            new DeliveryDto
            {
                DeliveryId       = delivery.DeliveryId,
                CurrentLocation  = delivery.CurrentLocation,
                Status           = delivery.Status,
                AssignedDriverId = delivery.AssignedDriverId,
                ExpectedEta      = delivery.ExpectedEta,
                ActualEta        = delivery.ActualEta,
                DistrictId       = delivery.DistrictId,
                AnomalyFlag      = delivery.AnomalyFlag,
                CreatedAt        = delivery.CreatedAt,
                PickedUpAt       = delivery.PickedUpAt,
                DeliveredAt      = delivery.DeliveredAt,
                CancelledAt      = delivery.CancelledAt,
                AnomalyReason    = delivery.AnomalyReason,
            },
            ct);
    }
}
