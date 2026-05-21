using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record ToggleDriverAvailabilityCommand(Guid DriverId, bool IsActive);

/// <summary>
/// Returns the availability response plus any domain events to cascade.
/// Wolverine automatically publishes the events and returns the first tuple element
/// when the caller uses bus.InvokeAsync&lt;DriverAvailabilityResponse?&gt;.
/// </summary>
public sealed class ToggleDriverAvailabilityHandler
{
    public async Task<(DriverAvailabilityResponse? Response, IEnumerable<object> Events)> Handle(
        ToggleDriverAvailabilityCommand command,
        IDriverRepository repository,
        CancellationToken ct)
    {
        var driver = await repository.GetByIdAsync(command.DriverId, ct);
        if (driver is null)
            return (null, Array.Empty<object>());

        var result = driver.SetAvailability(command.IsActive);
        if (result.IsFailure)
            return (null, Array.Empty<object>());

        await repository.UpdateAsync(driver, ct);

        var events = driver.DomainEvents.Cast<object>().ToList();
        driver.ClearDomainEvents();

        var status = driver.IsActive ? "available" : "offline";
        var response = new DriverAvailabilityResponse(
            driver.DriverId.ToString(),
            status,
            DateTime.UtcNow);

        return (response, events);
    }
}
