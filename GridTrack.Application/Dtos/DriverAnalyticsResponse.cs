namespace GridTrack.Application.Dtos;

public sealed record HourlyOnTimePointDto(int Hour, double OnTimeRatePct, int SampleCount);

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
    IReadOnlyList<HourlyOnTimePointDto> OnTimeByHour);

public sealed record GetDriverAnalyticsResponse(IReadOnlyList<DriverAnalyticsItemResponse> Drivers);
