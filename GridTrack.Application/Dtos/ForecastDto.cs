namespace GridTrack.Application.Dtos;

public sealed record ForecastDto(
    string DistrictId,
    DateTime ForecastWindow,
    int ExpectedDeliveries,
    DateTime GeneratedAt);

public sealed record StaffingForecastResponse(
    string   DistrictId,
    DateTime TargetAt,
    int      RecommendedDrivers,
    string   Confidence,          // "high" | "medium" | "low"
    string   Reasoning,
    double   HistoricalAvgDeliveries);
