using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record NearestDriversRequest(Point Location, int Count);

public sealed record GetNearestDriversQuery(NearestDriversRequest Request);

public sealed class GetNearestDriversHandler
{
    public async Task<Result<IEnumerable<DriverDto>>> Handle(
        GetNearestDriversQuery query,
        IDriverReadService readService,
        CancellationToken ct)
    {
        var drivers = await readService.GetNearestAsync(query.Request.Location, query.Request.Count, ct);
        return Result.Success(drivers);
    }
}
