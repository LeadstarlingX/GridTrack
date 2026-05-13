using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;
using System.Linq;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record CreateDriverRequest(
    Guid DriverId,
    Point Location,
    int H3Resolution,
    string? DistrictId,
    bool IsActive);

public sealed record CreateDriverCommand(CreateDriverRequest Request);

public sealed class CreateDriverHandler
{
    public async Task<(Result<DriverDto> Result, IEnumerable<object> Events)> Handle(
        CreateDriverCommand command,
        IDriverRepository repository,
        IH3GridService h3GridService,
        IDateTimeProvider dateTimeProvider,
        CancellationToken ct)
    {
        var request = command.Request;
        var districtId = request.DistrictId;

        if (string.IsNullOrWhiteSpace(districtId))
        {
            districtId = await h3GridService.GetCellIndexForPointAsync(request.Location, request.H3Resolution);
        }

        var driverResult = Driver.Create(
            request.DriverId,
            request.Location,
            districtId,
            dateTimeProvider.UtcNow,
            request.IsActive);

        if (driverResult.IsFailure)
        {
            return (Result.Failure<DriverDto>(driverResult.Error), Array.Empty<object>());
        }

        await repository.AddAsync(driverResult.Value, ct);
        var events = driverResult.Value.DomainEvents.Cast<object>().ToList();
        driverResult.Value.ClearDomainEvents();

        var dto = new DriverDto(
            driverResult.Value.DriverId,
            driverResult.Value.Location,
            driverResult.Value.IsActive,
            driverResult.Value.LastSeen,
            driverResult.Value.DistrictId);

        return (Result.Success(dto), events);
    }
}
