namespace GridTrack.Presentation.Controllers.Analytics;

public sealed record AnalyticsSummaryResponse(
    int TotalDeliveriesToday,
    double CompletionRate,
    int ActiveDrivers,
    double AnomalyRate,
    DateTime UpdatedAt);