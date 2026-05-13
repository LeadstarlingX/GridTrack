using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<T>(T domainEvent, CancellationToken ct) where T : IDomainEvent;
}
