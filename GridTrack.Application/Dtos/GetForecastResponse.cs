namespace GridTrack.Application.Dtos;

public sealed record GetForecastResponse(
    string DistrictId,
    int ForecastedDemand,
    string Horizon,
    int DriverRecommendation,
    double StaffingRatio,
    DateTime UpdatedAt);
