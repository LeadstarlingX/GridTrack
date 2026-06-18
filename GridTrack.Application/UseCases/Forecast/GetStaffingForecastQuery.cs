using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;

namespace GridTrack.Application.UseCases.Forecast;

public sealed record GetStaffingForecastQuery(
    string   DistrictId,
    DateTime TargetAt);

public sealed class GetStaffingForecastHandler
{
    public async Task<StaffingForecastResponse?> Handle(
        GetStaffingForecastQuery query,
        IAnalyticsReadService    analytics,
        IForecastService         forecastService,
        CancellationToken        ct)
    {
        var dayOfWeek = (int)query.TargetAt.DayOfWeek == 0 ? 6 : (int)query.TargetAt.DayOfWeek - 1;

        var historicalAvg = await analytics.GetHistoricalHourlyDeliveryAvgAsync(
            query.DistrictId, dayOfWeek, query.TargetAt.Hour, ct);

        // Surge flag: check if the district had a recent surge (last 15 min)
        // For now we pass false — could be wired to a cache key set by DemandSurgeHandler later.
        return await forecastService.GetStaffingAsync(
            query.DistrictId,
            query.TargetAt,
            historicalAvg,
            recentSurgeDetected: false,
            ct);
    }
}
