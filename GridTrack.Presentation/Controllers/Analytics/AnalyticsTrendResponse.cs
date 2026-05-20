namespace GridTrack.Presentation.Controllers.Analytics;

public sealed record AnalyticsTrendResponse(
    IReadOnlyList<AnalyticsTrendBucket> DeliveryTrend,
    IReadOnlyList<AnalyticsTrendBucket> AnomalyTrend);