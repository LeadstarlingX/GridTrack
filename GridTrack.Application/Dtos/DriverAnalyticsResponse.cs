namespace GridTrack.Application.Dtos;

public sealed record HourlyOnTimePoint(int Hour, double OnTimeRatePct, int SampleCount);

public sealed record DriverAnalyticsItemResponse(
    Guid DriverId,
    string Name,
    string? CarType,
    string DistrictId,
    int TotalLast7Days,
    int CompletedLast7Days,
    double? OnTimeRatePct,
    double AnomalyRate,
    double AvgDurationSeconds,
    double DistrictAvgDurationSeconds,
    IReadOnlyList<HourlyOnTimePoint> OnTimeByHour);

public sealed record GetDriverAnalyticsResponse(IReadOnlyList<DriverAnalyticsItemResponse> Drivers);
