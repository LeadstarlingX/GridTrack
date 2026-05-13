using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.EventHandlers;

public static class DomainEventDispatcher
{
    public static async Task PublishAsync(BaseEntity entity, IEventPublisher publisher, CancellationToken ct)
    {
        foreach (var domainEvent in entity.DomainEvents)
        {
            await publisher.PublishAsync(domainEvent, ct);
        }

        entity.ClearDomainEvents();
    }
}
