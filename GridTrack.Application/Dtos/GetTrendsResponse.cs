namespace GridTrack.Application.Dtos;

public sealed record TrendPointResponse(string Bucket, double Value);

public sealed record GetTrendsResponse(
    IReadOnlyList<TrendPointResponse> DeliveryTrend,
    IReadOnlyList<TrendPointResponse> AnomalyTrend,
    IReadOnlyList<TrendPointResponse> UrgencyTrend);
