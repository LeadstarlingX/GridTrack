using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Integration;
using GridTrack.Domain.Drivers;

namespace GridTrack.Application.EventHandlers;

public sealed class DriverAvailabilityChangedHandler
{
    public async Task<DriverAvailabilityChangedIntegrationEvent> Handle(
        DriverAvailabilityChangedDomainEvent domainEvent,
        CancellationToken ct)
    {
        var integrationEvent = new DriverAvailabilityChangedIntegrationEvent(
            domainEvent.DriverId,
            domainEvent.IsActive);

        return integrationEvent;
    }
}
