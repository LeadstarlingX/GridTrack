using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Common;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record GetDriversByDistrictQuery(DistrictFilterRequest Request);

public sealed class GetDriversByDistrictHandler
{
    public async Task<Result<IEnumerable<DriverDto>>> Handle(
        GetDriversByDistrictQuery query,
        IDriverReadService readService,
        CancellationToken ct)
    {
        var drivers = await readService.GetByDistrictAsync(query.Request.DistrictId, ct);
        return Result.Success(drivers);
    }
}
