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
    ICacheService cache) : IAnalyticsReadService
{
    // Live (today-inclusive) data uses a short TTL so the dashboard stays near-current;
    // safe to keep low because per-key single-flight collapses concurrent misses into one
    // query (no stampede). Historical ranges are immutable, so they keep a long TTL.
    private static readonly TimeSpan LiveTtl       = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HistoricalTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MapTtl        = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UtilTtl       = TimeSpan.FromSeconds(60);

    private static string Fmt(DateTime? dt) => dt?.ToString("yyyyMMdd") ?? "all";

    /// <summary>Short TTL when the date range still has live activity (includes today), long TTL otherwise.</summary>
    private static TimeSpan RangeTtl(DateTime? to)
    {
        var toDate = (to ?? DateTime.UtcNow).Date;
        return toDate >= DateTime.UtcNow.Date ? LiveTtl : HistoricalTtl;
    }

    public Task<GetAnalyticsSummaryResponse> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct)
        => cache.GetOrSetAsync(
            $"analytics:summary:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetSummaryAsync(from, to, innerCt),
            RangeTtl(to),
            ct);

    public Task<GetH3DensityResponse> GetH3DensityAsync(
        DateTime from, DateTime to, int resolution, int? fromHour, int? toHour, CancellationToken ct)
        // Keyed to the minute, not the day: rolling lookback windows (e.g. "last 3h") share a
        // day-truncated key with every other window requested that day, which would otherwise
        // serve a stale/wrong slider position for up to MapTtl after the first request.
        => cache.GetOrSetAsync(
            $"analytics:h3:{from:yyyyMMddHHmm}:{to:yyyyMMddHHmm}:{resolution}:{fromHour ?? -1}:{toHour ?? -1}",
            innerCt => inner.GetH3DensityAsync(from, to, resolution, fromHour, toHour, innerCt),
            MapTtl,
            ct);

    public Task<GetTrendsResponse> GetTrendsAsync(DateTime from, DateTime to, string granularity, CancellationToken ct)
        => cache.GetOrSetAsync(
            $"analytics:trends:{from:yyyyMMdd}:{to:yyyyMMdd}:{granularity}",
            innerCt => inner.GetTrendsAsync(from, to, granularity, innerCt),
            RangeTtl(to),
            ct);

    public Task<GetDistrictVolumeResponse> GetDistrictVolumeAsync(DateTime? from, DateTime? to, CancellationToken ct)
        => cache.GetOrSetAsync(
            $"analytics:district-vol:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetDistrictVolumeAsync(from, to, innerCt),
            RangeTtl(to),
            ct);

    public Task<GetCancellationAnalyticsResponse> GetCancellationAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct)
        => cache.GetOrSetAsync(
            $"analytics:cancellation:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetCancellationAnalyticsAsync(from, to, innerCt),
            RangeTtl(to),
            ct);

    public Task<GetDeliveryPerformanceResponse> GetDeliveryPerformanceAsync(DateTime? from, DateTime? to, CancellationToken ct)
        => cache.GetOrSetAsync(
            $"analytics:perf:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetDeliveryPerformanceAsync(from, to, innerCt),
            RangeTtl(to),
            ct);

    public Task<GetDriverUtilizationResponse> GetDriverUtilizationAsync(int topCount, CancellationToken ct)
        => cache.GetOrSetAsync(
            $"analytics:driver-util:{topCount}",
            innerCt => inner.GetDriverUtilizationAsync(topCount, innerCt),
            UtilTtl,
            ct);

    public Task<GetAnomalyBreakdownResponse> GetAnomalyBreakdownAsync(DateTime? from, DateTime? to, CancellationToken ct)
        => cache.GetOrSetAsync(
            $"analytics:anomaly-bd:{Fmt(from)}:{Fmt(to)}",
            innerCt => inner.GetAnomalyBreakdownAsync(from, to, innerCt),
            RangeTtl(to),
            ct);

    public Task<GetDriverAnalyticsResponse> GetDriverAnalyticsAsync(CancellationToken ct)
        => cache.GetOrSetAsync(
            "analytics:driver-analytics",
            innerCt => inner.GetDriverAnalyticsAsync(innerCt),
            LiveTtl,
            ct);

    public Task<double> GetHistoricalHourlyDeliveryAvgAsync(
        string districtId, int dayOfWeek, int hour, CancellationToken ct)
        => cache.GetOrSetAsync(
            $"analytics:hourly-avg:{districtId}:{dayOfWeek}:{hour}",
            innerCt => inner.GetHistoricalHourlyDeliveryAvgAsync(districtId, dayOfWeek, hour, innerCt),
            HistoricalTtl,
            ct);
}
