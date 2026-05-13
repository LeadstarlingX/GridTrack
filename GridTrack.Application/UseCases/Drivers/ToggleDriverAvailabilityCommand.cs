using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.EventHandlers;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record ToggleAvailabilityRequest(Guid DriverId, bool IsActive);

public sealed record ToggleDriverAvailabilityCommand(ToggleAvailabilityRequest Request);

public sealed class ToggleDriverAvailabilityHandler
{
    public async Task<OperationResult> Handle(
        ToggleDriverAvailabilityCommand command,
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

        var result = driver.SetAvailability(request.IsActive);
        if (result.IsFailure)
        {
            return OperationResult.From(result);
        }

        await repository.UpdateAsync(driver, ct);
        await DomainEventDispatcher.PublishAsync(driver, eventPublisher, ct);

        return OperationResult.Success();
    }
}
