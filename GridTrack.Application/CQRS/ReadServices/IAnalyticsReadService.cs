using GridTrack.Application.Dtos;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IAnalyticsReadService
{
    Task<GetAnalyticsSummaryResponse> GetSummaryAsync(CancellationToken ct);

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
}
