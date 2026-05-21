using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetTrendsQuery(DateTime From, DateTime To, string Granularity);

public sealed class GetTrendsHandler
{
    public Task<GetTrendsResponse> Handle(GetTrendsQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}
