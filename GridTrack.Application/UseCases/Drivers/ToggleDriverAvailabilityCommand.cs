using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using System.Linq;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record ToggleAvailabilityRequest(Guid DriverId, bool IsActive);

public sealed record ToggleDriverAvailabilityCommand(ToggleAvailabilityRequest Request);

public sealed class ToggleDriverAvailabilityHandler
{
    public async Task<(Result Result, IEnumerable<object> Events)> Handle(
        ToggleDriverAvailabilityCommand command,
        IDriverRepository repository,
        CancellationToken ct)
    {
        var request = command.Request;
        var driver = await repository.GetByIdAsync(request.DriverId, ct);

        if (driver is null)
        {
            return (Result.Failure(ApplicationErrors.DriverNotFound), Array.Empty<object>());
        }

        var result = driver.SetAvailability(request.IsActive);
        if (result.IsFailure)
        {
            return (result, Array.Empty<object>());
        }

        await repository.UpdateAsync(driver, ct);
        var events = driver.DomainEvents.Cast<object>().ToList();
        driver.ClearDomainEvents();

        return (Result.Success(), events);
    }
}
