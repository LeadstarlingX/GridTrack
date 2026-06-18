using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record GetDriverDetailQuery(Guid DriverId);

public sealed class GetDriverDetailHandler
{
    public async Task<DriverDetailResponse?> Handle(
        GetDriverDetailQuery query,
        IDriverReadService readService,
        CancellationToken ct)
    {
        var driver = await readService.GetAggregateByIdAsync(query.DriverId, ct);
        if (driver is null) return null;

        return new DriverDetailResponse(
            driver.DriverId,
            driver.Name,
            driver.ShortName,
            driver.DistrictId,
            driver.IsActive,
            driver.CarType?.ToString(),
            driver.LicensePlate,
            driver.PhoneNumber,
            driver.Location.Y,
            driver.Location.X,
            driver.LastSeen,
            driver.VehicleCapacityKg,
            driver.ShiftStartedAt,
            driver.ShiftEndsAt);
    }
}
