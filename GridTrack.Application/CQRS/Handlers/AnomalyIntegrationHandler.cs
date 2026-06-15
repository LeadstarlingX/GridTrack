using GridTrack.Application.IntegrationEvents;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;

namespace GridTrack.Application.CQRS.Handlers;

public static class AnomalyIntegrationHandler
{
    public static DeliveryAnomalyIntegrationEvent Map(DeliveryFlaggedAnomalousDomainEvent e)
        => new(
            e.DeliveryId,
            e.DistrictId,
            e.Type.ToString(),
            e.Reason,
            DriverLat: 0,
            DriverLng: 0,
            DateTime.UtcNow);

    public static DeliveryAnomalyIntegrationEvent Handle(DeliveryFlaggedAnomalousDomainEvent e)
        => Map(e);
}

public static class PositionIntegrationHandler
{
    public static DriverPositionIntegrationEvent Map(DriverPositionUpdatedDomainEvent e)
        => new(
            e.DriverId,
            e.DistrictId,
            Lat: e.Location.Y,
            Lng: e.Location.X,
            DeliveryStatus: "InTransit",
            e.Timestamp);

    public static DriverPositionIntegrationEvent Handle(DriverPositionUpdatedDomainEvent e)
        => Map(e);
}

public static class CompletedIntegrationHandler
{
    public static DeliveryCompletedIntegrationEvent? Map(DeliveryCompletedDomainEvent e)
    {
        if (e.DriverId is null || e.PickedUpAt is null)
            return null;

        var actual = (e.DeliveredAt - e.PickedUpAt.Value).TotalSeconds;

        return new DeliveryCompletedIntegrationEvent(
            e.DeliveryId,
            e.DriverId.Value,
            e.DistrictId,
            e.PickedUpAt.Value,
            e.DeliveredAt,
            actual,
            e.ExpectedDurationSeconds);
    }

    public static DeliveryCompletedIntegrationEvent? Handle(DeliveryCompletedDomainEvent e)
        => Map(e);
}
