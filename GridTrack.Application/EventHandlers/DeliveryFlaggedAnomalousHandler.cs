using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Integration;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.EventHandlers;

public sealed class DeliveryFlaggedAnomalousHandler
{
    public async Task<DeliveryFlaggedAnomalousIntegrationEvent> Handle(
        DeliveryFlaggedAnomalousDomainEvent domainEvent, 
        CancellationToken ct)
    {
        var integrationEvent = new DeliveryFlaggedAnomalousIntegrationEvent(
            domainEvent.DeliveryId,
            domainEvent.Type,
            domainEvent.Reason);

        return integrationEvent;
    }
}
