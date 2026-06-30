using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetDistrictDemandForecastQuery(int HoursAhead);

public sealed class GetDistrictDemandForecastHandler
{
    public Task<GetDistrictDemandForecastResponse> Handle(
        GetDistrictDemandForecastQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetDistrictDemandForecastAsync(query.HoursAhead, ct);
}
