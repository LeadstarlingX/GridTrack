using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Integration;
using GridTrack.Domain.Drivers;

namespace GridTrack.Application.EventHandlers;

public sealed class DriverAvailabilityChangedHandler
{
    public Task Handle(DriverAvailabilityChangedDomainEvent domainEvent, IEventPublisher publisher, CancellationToken ct)
    {
        var integrationEvent = new DriverAvailabilityChangedIntegrationEvent(
            domainEvent.DriverId,
            domainEvent.IsActive);

        return publisher.PublishAsync(integrationEvent, ct);
    }
}
