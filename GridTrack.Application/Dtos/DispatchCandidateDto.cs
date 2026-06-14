namespace GridTrack.Application.Dtos;

public sealed record DispatchCandidateDto(
    Guid   DriverId,
    string Name,
    string ShortName,
    string DistrictId,
    double DistanceM,
    double? OnTimeRatePct,
    int    ActiveDeliveries,
    double ShiftScore,
    double Score);
