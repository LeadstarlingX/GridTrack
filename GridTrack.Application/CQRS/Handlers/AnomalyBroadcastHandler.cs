using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.CQRS.Handlers;

public static class AnomalyBroadcastHandler
{
    public static Task Handle(
        DeliveryFlaggedAnomalousDomainEvent e,
        IDashboardPushService push,
        CancellationToken ct)
        => push.BroadcastAnomalyAsync(
            e.DistrictId,
            new AnomalyAlertDto
            {
                DeliveryId = e.DeliveryId,
                DistrictId = e.DistrictId,
                Type       = e.Type,
                Reason     = e.Reason,
                Timestamp  = DateTime.UtcNow,
            },
            ct);
}
