using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.EventHandlers;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record UpdatePositionRequest(Guid DriverId, Point Location, DateTime Timestamp);

public sealed record UpdateDriverPositionCommand(UpdatePositionRequest Request);

public sealed class UpdateDriverPositionHandler
{
    public async Task<OperationResult> Handle(
        UpdateDriverPositionCommand command,
        IDriverRepository repository,
        IEventPublisher eventPublisher,
        CancellationToken ct)
    {
        var request = command.Request;
        var driver = await repository.GetByIdAsync(request.DriverId, ct);

        if (driver is null)
        {
            return OperationResult.Failure(ApplicationErrors.DriverNotFound);
        }

        var result = driver.UpdatePosition(request.Location, request.Timestamp);
        if (result.IsFailure)
        {
            return OperationResult.From(result);
        }

        await repository.UpdateAsync(driver, ct);
        await DomainEventDispatcher.PublishAsync(driver, eventPublisher, ct);

        return OperationResult.Success();
    }
}
