using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Drivers;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record ToggleDriverAvailabilityCommand(Guid DriverId, bool IsActive);


public sealed class ToggleDriverAvailabilityHandler
{
    public async Task<(DriverAvailabilityResponse? Response, DriverAvailabilityChangedDomainEvent? Event)> Handle(
        ToggleDriverAvailabilityCommand command,
        IDriverReadService readService,
        IDriverRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var driver = await readService.GetAggregateByIdAsync(command.DriverId, ct);
        if (driver is null)
            return (null, null);

        var result = driver.SetAvailability(command.IsActive);
        if (result.IsFailure)
            return (null, null);

        await repository.UpdateAsync(driver, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var domainEvent = driver.DomainEvents
            .OfType<DriverAvailabilityChangedDomainEvent>()
            .FirstOrDefault();
        driver.ClearDomainEvents();

        var status = driver.IsActive ? "available" : "offline";
        var response = new DriverAvailabilityResponse(
            driver.DriverId.ToString(),
            status,
            DateTime.UtcNow);

        return (response, domainEvent);
    }
}
