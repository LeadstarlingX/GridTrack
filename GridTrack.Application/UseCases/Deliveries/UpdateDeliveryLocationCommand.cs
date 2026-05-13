using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.EventHandlers;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record UpdateLocationRequest(Guid DeliveryId, Point Location, DateTime Timestamp);

public sealed record UpdateDeliveryLocationCommand(UpdateLocationRequest Request);

public sealed class UpdateDeliveryLocationHandler
{
    public async Task<OperationResult> Handle(
        UpdateDeliveryLocationCommand command,
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

        var updateResult = delivery.UpdateLocation(request.Location, request.Timestamp);
        if (updateResult.IsFailure)
        {
            return OperationResult.From(updateResult);
        }

        await repository.UpdateAsync(delivery, ct);
        await DomainEventDispatcher.PublishAsync(delivery, eventPublisher, ct);

        return OperationResult.Success();
    }
}
