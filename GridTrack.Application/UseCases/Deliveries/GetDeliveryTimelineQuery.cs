using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record GetDeliveryTimelineQuery(Guid DeliveryId);

public sealed record DeliveryTimelineEventDto(
    string Type,
    string Label,
    DateTime? At,
    string? Note);

public sealed record DeliveryTimelineResponse(
    Guid DeliveryId,
    IReadOnlyList<DeliveryTimelineEventDto> Events);

public sealed class GetDeliveryTimelineHandler
{
    public async Task<DeliveryTimelineResponse?> Handle(
        GetDeliveryTimelineQuery query,
        IDeliveryReadService readService,
        CancellationToken ct)
    {
        var d = await readService.GetByIdAsync(query.DeliveryId, ct);
        if (d is null) return null;

        var events = new List<DeliveryTimelineEventDto>
        {
            new("Created", "Order placed", d.CreatedAt, null),
        };

        if (d.AssignedDriverId.HasValue)
            events.Add(new("Assigned", "Driver assigned", null, null));

        if (d.PickedUpAt.HasValue)
            events.Add(new("PickedUp", "Picked up", d.PickedUpAt, null));

        if (d.Status >= DeliveryStatus.InTransit && d.DeliveredAt is null && d.CancelledAt is null)
            events.Add(new("InTransit", "In transit", null, null));

        if (d.DeliveredAt.HasValue)
        {
            string? note = null;
            if (d.ExpectedEta.HasValue)
            {
                var diffMin = (int)(d.DeliveredAt.Value - d.ExpectedEta.Value).TotalMinutes;
                note = diffMin <= 0
                    ? $"{Math.Abs(diffMin)} min ahead of ETA"
                    : $"{diffMin} min late";
            }
            events.Add(new("Delivered", "Delivered", d.DeliveredAt, note));
        }

        if (d.CancelledAt.HasValue)
            events.Add(new("Cancelled", "Cancelled", d.CancelledAt, d.AnomalyReason));

        if (d.AnomalyFlag)
        {
            var anomalyLabel = d.AnomalyTypeValue switch
            {
                AnomalyType.EtaExceeded    => "ETA exceeded",
                AnomalyType.RouteDeviation => "Route deviation",
                AnomalyType.UnexpectedStop => "Unexpected stop",
                _                          => "Anomaly detected",
            };
            // For cancelled-with-anomaly the anomaly event is at CancelledAt,
            // for in-transit anomalies we don't have a precise timestamp.
            var anomalyAt = d.Status == DeliveryStatus.Cancelled ? d.CancelledAt : null;
            events.Add(new("AnomalyFlagged", anomalyLabel, anomalyAt, d.AnomalyReason));
        }

        return new DeliveryTimelineResponse(query.DeliveryId, events);
    }
}
