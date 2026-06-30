using GridTrack.Application.Dtos;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IAnalyticsReadService
{
    Task<GetAnalyticsSummaryResponse> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct);

    Task<GetPickupDensityResponse> GetPickupDensityAsync(
        DateTime from,
        DateTime to,
        int? fromHour,
        int? toHour,
        CancellationToken ct);

    Task<GetTrendsResponse> GetTrendsAsync(
        DateTime from,
        DateTime to,
        string granularity,
        CancellationToken ct);

    Task<GetDistrictVolumeResponse> GetDistrictVolumeAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken ct);

    Task<GetCancellationAnalyticsResponse> GetCancellationAnalyticsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken ct);

    Task<GetDeliveryPerformanceResponse> GetDeliveryPerformanceAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken ct);

    Task<GetDriverUtilizationResponse> GetDriverUtilizationAsync(
        int topCount,
        CancellationToken ct);

    Task<GetAnomalyBreakdownResponse> GetAnomalyBreakdownAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken ct);

    Task<GetDriverAnalyticsResponse> GetDriverAnalyticsAsync(CancellationToken ct);

    /// <summary>Average deliveries created at the given hour/day-of-week over the past 28 days.</summary>
    Task<double> GetHistoricalHourlyDeliveryAvgAsync(
        string districtId,
        int    dayOfWeek,
        int    hour,
        CancellationToken ct);

    /// <summary>
    /// Per-district predicted delivery volume over the next <paramref name="hoursAhead"/> hours,
    /// ranked descending. A simple seasonal-naive forecast: sums the 28-day historical
    /// hour-of-day/day-of-week average for each of the upcoming hours, per district.
    /// </summary>
    Task<GetDistrictDemandForecastResponse> GetDistrictDemandForecastAsync(
        int hoursAhead,
        CancellationToken ct);
}
