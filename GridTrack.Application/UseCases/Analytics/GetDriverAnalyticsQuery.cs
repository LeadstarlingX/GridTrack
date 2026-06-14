using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetDriverAnalyticsQuery;

public sealed class GetDriverAnalyticsHandler
{
    public Task<GetDriverAnalyticsResponse> Handle(
        GetDriverAnalyticsQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetDriverAnalyticsAsync(ct);
}
