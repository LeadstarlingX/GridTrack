using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetAnalyticsSummaryQuery();

public sealed class GetAnalyticsSummaryHandler
{
    public Task<GetAnalyticsSummaryResponse> Handle(
        GetAnalyticsSummaryQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetSummaryAsync(ct);
}
