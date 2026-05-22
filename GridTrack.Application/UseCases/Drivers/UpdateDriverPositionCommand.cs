using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Errors;
using GridTrack.Domain.Abstractions;
using NetTopologySuite.Geometries;
using System.Linq;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record UpdatePositionRequest(Guid DriverId, Point Location, DateTime Timestamp);

public sealed record UpdateDriverPositionCommand(UpdatePositionRequest Request);

public sealed class UpdateDriverPositionHandler
{
    public async Task<(Result Result, IEnumerable<object> Events)> Handle(
        UpdateDriverPositionCommand command,
        IDriverReadService readService,
        IDriverRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var request = command.Request;
        var driver = await readService.GetAggregateByIdAsync(request.DriverId, ct);

        if (driver is null)
        {
            return (Result.Failure(ApplicationErrors.DriverNotFound), Array.Empty<object>());
        }

        var result = driver.UpdatePosition(request.Location, request.Timestamp);
        if (result.IsFailure)
        {
            return (result, Array.Empty<object>());
        }

        await repository.UpdateAsync(driver, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var events = driver.DomainEvents.Cast<object>().ToList();
        driver.ClearDomainEvents();

        return (Result.Success(), events);
    }
}
