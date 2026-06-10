using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetCancellationAnalyticsQuery(DateTime? From, DateTime? To);

public sealed class GetCancellationAnalyticsHandler
{
    public Task<GetCancellationAnalyticsResponse> Handle(
        GetCancellationAnalyticsQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetCancellationAnalyticsAsync(query.From, query.To, ct);
}
