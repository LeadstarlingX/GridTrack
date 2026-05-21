using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Forecast;

public sealed record GetForecastQuery(string DistrictId);

public sealed class GetForecastHandler
{
    public Task<GetForecastResponse?> Handle(GetForecastQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}
