using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetDeliveryPerformanceQuery(DateTime? From, DateTime? To);

public sealed class GetDeliveryPerformanceHandler
{
    public Task<GetDeliveryPerformanceResponse> Handle(
        GetDeliveryPerformanceQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetDeliveryPerformanceAsync(query.From, query.To, ct);
}
