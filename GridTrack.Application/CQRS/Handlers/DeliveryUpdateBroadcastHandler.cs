using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.CQRS.Handlers;

public static class DeliveryUpdateBroadcastHandler
{
    public static Task Handle(DeliveryAssignedDomainEvent e, IDeliveryReadService r, IDashboardPushService p, CancellationToken ct)
        => BroadcastAsync(e.DeliveryId, r, p, ct);

    public static Task Handle(DeliveryPickedUpDomainEvent e, IDeliveryReadService r, IDashboardPushService p, CancellationToken ct)
    {
        Console.WriteLine($"[BROADCAST-HANDLER] DeliveryPickedUpDomainEvent received for {e.DeliveryId}");
        return BroadcastAsync(e.DeliveryId, r, p, ct);
    }

    public static Task Handle(DeliveryLocationUpdatedDomainEvent e, IDeliveryReadService r, IDashboardPushService p, CancellationToken ct)
        => BroadcastAsync(e.DeliveryId, r, p, ct);

    public static Task Handle(DeliveryCompletedDomainEvent e, IDeliveryReadService r, IDashboardPushService p, CancellationToken ct)
        => BroadcastAsync(e.DeliveryId, r, p, ct);

    public static Task Handle(DeliveryCancelledDomainEvent e, IDeliveryReadService r, IDashboardPushService p, CancellationToken ct)
        => BroadcastAsync(e.DeliveryId, r, p, ct);

    private static async Task BroadcastAsync(Guid deliveryId, IDeliveryReadService r, IDashboardPushService p, CancellationToken ct)
    {
        var delivery = await r.GetAggregateByIdAsync(deliveryId, ct);
        Console.WriteLine($"[BROADCAST-HANDLER] Read delivery: {delivery is not null}, district: {delivery?.DistrictId}");
        if (delivery is null) return;

        await p.BroadcastDeliveryUpdateAsync(
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
