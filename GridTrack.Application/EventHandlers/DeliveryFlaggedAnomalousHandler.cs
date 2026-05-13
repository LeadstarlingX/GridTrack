using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Integration;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.EventHandlers;

public sealed class DeliveryFlaggedAnomalousHandler
{
    public Task Handle(DeliveryFlaggedAnomalousDomainEvent domainEvent, IEventPublisher publisher, CancellationToken ct)
    {
        var integrationEvent = new DeliveryFlaggedAnomalousIntegrationEvent(
            domainEvent.DeliveryId,
            domainEvent.Type,
            domainEvent.Reason);

        return publisher.PublishAsync(integrationEvent, ct);
    }
}
