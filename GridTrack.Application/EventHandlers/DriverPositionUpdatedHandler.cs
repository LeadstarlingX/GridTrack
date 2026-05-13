using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Integration;
using GridTrack.Domain.Drivers;

namespace GridTrack.Application.EventHandlers;

public sealed class DriverPositionUpdatedHandler
{
    public async Task<DriverPositionUpdatedIntegrationEvent> Handle(
        DriverPositionUpdatedDomainEvent domainEvent,
        CancellationToken ct)
    {
        var integrationEvent = new DriverPositionUpdatedIntegrationEvent(
            domainEvent.DriverId,
            domainEvent.Location,
            domainEvent.Timestamp);

        return integrationEvent;
    }
}
