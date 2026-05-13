using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Integration;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.EventHandlers;

public sealed class DeliveryCreatedHandler
{
    public async Task<DeliveryCreatedIntegrationEvent> Handle(
        DeliveryCreatedDomainEvent domainEvent,
        CancellationToken ct)
    {
        var integrationEvent = new DeliveryCreatedIntegrationEvent(
            domainEvent.DeliveryId,
            domainEvent.DistrictId,
            domainEvent.CreatedAt);

        return integrationEvent;
    }
}
