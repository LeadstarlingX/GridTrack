using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Domain.Abstractions;

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
        IDriverReadService readService,
        IDriverRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var driver = await readService.GetAggregateByIdAsync(command.DriverId, ct);
        if (driver is null)
            return (null, Array.Empty<object>());

        var result = driver.SetAvailability(command.IsActive);
        if (result.IsFailure)
            return (null, Array.Empty<object>());

        await repository.UpdateAsync(driver, ct);
        await unitOfWork.SaveChangesAsync(ct);

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
