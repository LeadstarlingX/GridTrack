using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetAnomalyBreakdownQuery(DateTime? From, DateTime? To);

public sealed class GetAnomalyBreakdownHandler
{
    public Task<GetAnomalyBreakdownResponse> Handle(
        GetAnomalyBreakdownQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetAnomalyBreakdownAsync(query.From, query.To, ct);
}
