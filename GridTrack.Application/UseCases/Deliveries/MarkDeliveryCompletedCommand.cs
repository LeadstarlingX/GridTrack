using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using System.Linq;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record CompleteDeliveryRequest(Guid DeliveryId, DateTime Timestamp);

public sealed record MarkDeliveryCompletedCommand(CompleteDeliveryRequest Request);

public sealed class MarkDeliveryCompletedHandler
{
    public async Task<(Result Result, IEnumerable<object> Events)> Handle(
        MarkDeliveryCompletedCommand command,
        IDeliveryRepository repository,
        CancellationToken ct)
    {
        var request = command.Request;
        var delivery = await repository.GetByIdAsync(request.DeliveryId, ct);

        if (delivery is null)
        {
            return (Result.Failure(ApplicationErrors.DeliveryNotFound), Array.Empty<object>());
        }

        var result = delivery.MarkDelivered(request.Timestamp);
        if (result.IsFailure)
        {
            return (result, Array.Empty<object>());
        }

        await repository.UpdateAsync(delivery, ct);
        var events = delivery.DomainEvents.Cast<object>().ToList();
        delivery.ClearDomainEvents();

        return (Result.Success(), events);
    }
}
