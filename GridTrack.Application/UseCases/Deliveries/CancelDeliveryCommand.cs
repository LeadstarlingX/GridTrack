using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.EventHandlers;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record CancelDeliveryRequest(Guid DeliveryId, DateTime Timestamp, string Reason);

public sealed record CancelDeliveryCommand(CancelDeliveryRequest Request);

public sealed class CancelDeliveryHandler
{
    public async Task<OperationResult> Handle(
        CancelDeliveryCommand command,
        IDeliveryRepository repository,
        IEventPublisher eventPublisher,
        CancellationToken ct)
    {
        var request = command.Request;
        var delivery = await repository.GetByIdAsync(request.DeliveryId, ct);

        if (delivery is null)
        {
            return OperationResult.Failure(ApplicationErrors.DeliveryNotFound);
        }

        var result = delivery.MarkCancelled(request.Timestamp, request.Reason);
        if (result.IsFailure)
        {
            return OperationResult.From(result);
        }

        await repository.UpdateAsync(delivery, ct);
        await DomainEventDispatcher.PublishAsync(delivery, eventPublisher, ct);

        return OperationResult.Success();
    }
}
