using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.EventHandlers;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.ValueObjects;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record FlagAnomalyRequest(Guid DeliveryId, AnomalyType Type, string Reason);

public sealed record FlagDeliveryAnomalyCommand(FlagAnomalyRequest Request);

public sealed class FlagDeliveryAnomalyHandler
{
    public async Task<OperationResult> Handle(
        FlagDeliveryAnomalyCommand command,
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

        var result = delivery.FlagAnomaly(request.Type, request.Reason);
        if (result.IsFailure)
        {
            return OperationResult.From(result);
        }

        await repository.UpdateAsync(delivery, ct);
        await DomainEventDispatcher.PublishAsync(delivery, eventPublisher, ct);

        return OperationResult.Success();
    }
}
