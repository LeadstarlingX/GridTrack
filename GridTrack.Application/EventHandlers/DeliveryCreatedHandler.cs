using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Integration;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.EventHandlers;

public sealed class DeliveryCreatedHandler
{
    public Task Handle(DeliveryCreatedDomainEvent domainEvent, IEventPublisher publisher, CancellationToken ct)
    {
        var integrationEvent = new DeliveryCreatedIntegrationEvent(
            domainEvent.DeliveryId,
            domainEvent.DistrictId,
            domainEvent.CreatedAt);

        return publisher.PublishAsync(integrationEvent, ct);
    }
}
