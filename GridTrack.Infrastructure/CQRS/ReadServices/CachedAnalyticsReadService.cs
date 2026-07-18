using GridTrack.Application.Abstractions.Authentication;
using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

/// <summary>
/// Cache-aside decorator for IAnalyticsReadService.
/// TTL is adaptive: ranges that include today use a short TTL (data is still changing);
/// fully historical ranges use a long TTL (data is immutable).
/// </summary>
internal sealed class CachedAnalyticsReadService(
    AnalyticsReadService inner,
    ICacheService cache,
    ICurrentUser currentUser) : IAnalyticsReadService
{
    private static readonly TimeSpan LiveTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HistoricalTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MapTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UtilTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ForecastTtl = TimeSpan.FromSeconds(60);

    private static string Fmt(DateTime? dt) => dt?.ToString("yyyyMMdd") ?? "all";

    private static TimeSpan RangeTtl(DateTime? to)
    {
        var toDate = (to ?? DateTime.UtcNow).Date;
        return toDate >= DateTime.UtcNow.Date ? LiveTtl : HistoricalTtl;
    }

    // Bypass cache for Observers — they carry sector-specific AllowedDistrictIds and must
    // never read or populate keys that are shared with other users.
    private Task<T> Get<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct)
        => currentUser.IsGeneralObserver
            ? cache.GetOrSetAsync(key, factory, ttl, ct)
            : factory(ct);

    public Task<GetAnalyticsSummaryResponse> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct)
        => Get($"analytics:summary:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetSummaryAsync(from, to, innerCt), RangeTtl(to), ct);

    public Task<GetPickupDensityResponse> GetPickupDensityAsync(
        DateTime from, DateTime to, int? fromHour, int? toHour, CancellationToken ct)
        => Get($"analytics:pickup-density:{from:yyyyMMddHHmm}:{to:yyyyMMddHHmm}:{fromHour ?? -1}:{toHour ?? -1}",
            innerCt => inner.GetPickupDensityAsync(from, to, fromHour, toHour, innerCt), MapTtl, ct);

    public Task<GetTrendsResponse> GetTrendsAsync(DateTime from, DateTime to, string granularity, CancellationToken ct)
        => Get($"analytics:trends:{from:yyyyMMdd}:{to:yyyyMMdd}:{granularity}",
            innerCt => inner.GetTrendsAsync(from, to, granularity, innerCt), RangeTtl(to), ct);

    public Task<GetDistrictVolumeResponse> GetDistrictVolumeAsync(DateTime? from, DateTime? to, CancellationToken ct)
        => Get($"analytics:district-vol:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetDistrictVolumeAsync(from, to, innerCt), RangeTtl(to), ct);

    public Task<GetCancellationAnalyticsResponse> GetCancellationAnalyticsAsync(DateTime? from, DateTime? to,
        CancellationToken ct)
        => Get($"analytics:cancellation:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetCancellationAnalyticsAsync(from, to, innerCt), RangeTtl(to), ct);

    public Task<GetDeliveryPerformanceResponse> GetDeliveryPerformanceAsync(DateTime? from, DateTime? to,
        CancellationToken ct)
        => Get($"analytics:perf:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetDeliveryPerformanceAsync(from, to, innerCt), RangeTtl(to), ct);

    public Task<GetDriverUtilizationResponse> GetDriverUtilizationAsync(int topCount, CancellationToken ct)
        => Get($"analytics:driver-util:{topCount}",
            innerCt => inner.GetDriverUtilizationAsync(topCount, innerCt), UtilTtl, ct);

    public Task<GetAnomalyBreakdownResponse> GetAnomalyBreakdownAsync(DateTime? from, DateTime? to,
        CancellationToken ct)
        => Get($"analytics:anomaly-bd:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetAnomalyBreakdownAsync(from, to, innerCt), RangeTtl(to), ct);

    public Task<GetDriverAnalyticsResponse> GetDriverAnalyticsAsync(CancellationToken ct)
        => Get("analytics:driver-analytics",
            innerCt => inner.GetDriverAnalyticsAsync(innerCt), LiveTtl, ct);

    public Task<double> GetHistoricalHourlyDeliveryAvgAsync(
        string districtId, int dayOfWeek, int hour, CancellationToken ct)
        => Get($"analytics:hourly-avg:{districtId}:{dayOfWeek}:{hour}",
            innerCt => inner.GetHistoricalHourlyDeliveryAvgAsync(districtId, dayOfWeek, hour, innerCt),
            HistoricalTtl, ct);

    public Task<GetDistrictDemandForecastResponse> GetDistrictDemandForecastAsync(int hoursAhead, CancellationToken ct)
        => Get($"analytics:district-forecast:{hoursAhead}:{DateTime.UtcNow:yyyyMMddHH}",
            innerCt => inner.GetDistrictDemandForecastAsync(hoursAhead, innerCt), ForecastTtl, ct);
}