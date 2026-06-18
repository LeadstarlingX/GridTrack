using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Drivers;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record ToggleDriverAvailabilityCommand(Guid DriverId, bool IsActive);


public sealed class ToggleDriverAvailabilityHandler
{
    public async Task<(Result<DriverAvailabilityResponse> Result, DriverAvailabilityChangedDomainEvent? Event)> Handle(
        ToggleDriverAvailabilityCommand command,
        IDriverReadService readService,
        IDriverRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var driver = await readService.GetAggregateByIdAsync(command.DriverId, ct);
        if (driver is null)
            return (Result.Failure<DriverAvailabilityResponse>(ApplicationErrors.DriverNotFound), null);

        var setResult = driver.SetAvailability(command.IsActive);
        if (setResult.IsFailure)
            return (Result.Failure<DriverAvailabilityResponse>(setResult.Error), null);

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

        return (Result.Success(response), domainEvent);
    }
}
