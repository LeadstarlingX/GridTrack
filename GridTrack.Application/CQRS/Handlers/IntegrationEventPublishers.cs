using GridTrack.Application.IntegrationEvents;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;

namespace GridTrack.Application.CQRS.Handlers;

public static class AnomalyIntegrationPublisher
{
    public static DeliveryAnomalyIntegrationEvent Handle(
        DeliveryFlaggedAnomalousDomainEvent e)
        => new(
            e.DeliveryId,
            e.DistrictId,
            e.Type.ToString(),
            e.Reason,
            DriverLat: 0,   // driver location not carried on delivery aggregate
            DriverLng: 0,
            DateTime.UtcNow);
}

public static class PositionIntegrationPublisher
{
    public static DriverPositionIntegrationEvent Handle(
        DriverPositionUpdatedDomainEvent e)
        => new(
            e.DriverId,
            e.DistrictId,
            Lat: e.Location.Y,
            Lng: e.Location.X,
            DeliveryStatus: "InTransit",  // delivery status not carried on driver aggregate
            e.Timestamp);
}

public static class CompletedIntegrationPublisher
{
    public static DeliveryCompletedIntegrationEvent? Handle(
        DeliveryCompletedDomainEvent e)
    {
        // Skip if the delivery had no assigned driver (shouldn't happen in practice)
        if (e.DriverId is null || e.PickedUpAt is null)
            return null;

        var actual = (e.DeliveredAt - e.PickedUpAt.Value).TotalSeconds;

        return new DeliveryCompletedIntegrationEvent(
            e.DeliveryId,
            e.DriverId.Value,
            DistrictId: string.Empty,  // not on DeliveryCompletedDomainEvent; enrichable if needed
            e.PickedUpAt.Value,
            e.DeliveredAt,
            actual,
            e.ExpectedDurationSeconds);
    }
}
