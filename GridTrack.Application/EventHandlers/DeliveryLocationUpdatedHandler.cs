using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Integration;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.EventHandlers;

public sealed class DeliveryLocationUpdatedHandler
{
    public async Task<DeliveryLocationUpdatedIntegrationEvent> Handle(DeliveryLocationUpdatedDomainEvent domainEvent,
        CancellationToken ct)
    {
        var integrationEvent = new DeliveryLocationUpdatedIntegrationEvent(
            domainEvent.DeliveryId,
            domainEvent.Location,
            domainEvent.Timestamp);

        return integrationEvent;
    }
}
