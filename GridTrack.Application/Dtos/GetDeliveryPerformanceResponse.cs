namespace GridTrack.Application.Dtos;

public sealed record DistrictPerformanceItemResponse(
    string DistrictId,
    int DeliveredCount,
    double AvgActualDurationSeconds,
    double AvgExpectedDurationSeconds,
    double OnTimeRate);

public sealed record GetDeliveryPerformanceResponse(
    int DeliveredCount,
    double OverallOnTimeRate,
    double OverallAvgDurationSeconds,
    IReadOnlyList<DistrictPerformanceItemResponse> Districts);
