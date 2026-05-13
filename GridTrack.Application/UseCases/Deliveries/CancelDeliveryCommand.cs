using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using System.Linq;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record CancelDeliveryRequest(Guid DeliveryId, DateTime Timestamp, string Reason);

public sealed record CancelDeliveryCommand(CancelDeliveryRequest Request);

public sealed class CancelDeliveryHandler
{
    public async Task<(Result Result, IEnumerable<object> Events)> Handle(
        CancelDeliveryCommand command,
        IDeliveryRepository repository,
        CancellationToken ct)
    {
        var request = command.Request;
        var delivery = await repository.GetByIdAsync(request.DeliveryId, ct);

        if (delivery is null)
        {
            return (Result.Failure(ApplicationErrors.DeliveryNotFound), Array.Empty<object>());
        }

        var result = delivery.MarkCancelled(request.Timestamp, request.Reason);
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
