using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Districts;

public sealed record GetDistrictsQuery();

public sealed class GetDistrictsHandler
{
    public Task<GetDistrictsResponse> Handle(GetDistrictsQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}
