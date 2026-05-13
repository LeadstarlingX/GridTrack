using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.EventHandlers;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record AssignDriverRequest(Guid DeliveryId, Guid DriverId);

public sealed record AssignDriverToDeliveryCommand(AssignDriverRequest Request);

public sealed class AssignDriverToDeliveryHandler
{
    public async Task<OperationResult> Handle(
        AssignDriverToDeliveryCommand command,
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

        var result = delivery.AssignDriver(request.DriverId);
        if (result.IsFailure)
        {
            return OperationResult.From(result);
        }

        await repository.UpdateAsync(delivery, ct);
        await DomainEventDispatcher.PublishAsync(delivery, eventPublisher, ct);

        return OperationResult.Success();
    }
}
