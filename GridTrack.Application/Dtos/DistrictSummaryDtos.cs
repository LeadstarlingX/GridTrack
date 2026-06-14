namespace GridTrack.Application.Dtos;

public sealed record DistrictContextDto(
    string DistrictId,
    int    ActiveDeliveries,
    int    ActiveDrivers,
    double AnomalyRate24h);

public sealed record DistrictSummaryResponse(
    string    DistrictId,
    string    Summary,
    DateTime  GeneratedAt,
    DateTime? CachedAt);   // null = fresh AI result; non-null = stale (last known good)
