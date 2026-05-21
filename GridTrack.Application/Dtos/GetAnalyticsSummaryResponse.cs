namespace GridTrack.Application.Dtos;

public sealed record GetAnalyticsSummaryResponse(
    int TotalDeliveriesToday,
    double CompletionRate,
    int ActiveDrivers,
    double AnomalyRate,
    DateTime UpdatedAt);
