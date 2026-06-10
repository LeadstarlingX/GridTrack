using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetDriverUtilizationQuery(int TopCount);

public sealed class GetDriverUtilizationHandler
{
    public Task<GetDriverUtilizationResponse> Handle(
        GetDriverUtilizationQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetDriverUtilizationAsync(query.TopCount, ct);
}
