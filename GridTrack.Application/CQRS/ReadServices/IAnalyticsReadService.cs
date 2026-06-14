using GridTrack.Application.Dtos;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IAnalyticsReadService
{
    Task<GetAnalyticsSummaryResponse> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct);

    Task<GetH3DensityResponse> GetH3DensityAsync(
        DateTime from,
        DateTime to,
        int resolution,
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
}
