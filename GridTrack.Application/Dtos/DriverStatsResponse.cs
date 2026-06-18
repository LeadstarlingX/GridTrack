namespace GridTrack.Application.Dtos;

public sealed record DriverStatsResponse(
    Guid DriverId,
    string Name,
    int TotalCompleted,
    int CompletedToday,
    int TotalCancelled,
    int ActiveDeliveries,
    double OnTimeRatePct);
