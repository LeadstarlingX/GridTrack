using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetTrendsQuery(DateTime From, DateTime To, string Granularity);

public sealed class GetTrendsHandler
{
    public Task<GetTrendsResponse> Handle(
        GetTrendsQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetTrendsAsync(query.From, query.To, query.Granularity, ct);
}
