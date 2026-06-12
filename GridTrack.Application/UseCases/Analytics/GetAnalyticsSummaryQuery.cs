using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetAnalyticsSummaryQuery(DateTime? From = null, DateTime? To = null);

public sealed class GetAnalyticsSummaryHandler
{
    public Task<GetAnalyticsSummaryResponse> Handle(
        GetAnalyticsSummaryQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetSummaryAsync(query.From, query.To, ct);
}
