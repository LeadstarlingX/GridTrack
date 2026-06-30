namespace GridTrack.Application.Dtos;

public sealed record DistrictDemandForecastItemResponse(
    string DistrictId,
    string DistrictName,
    double PredictedDeliveries);

public sealed record GetDistrictDemandForecastResponse(
    IReadOnlyList<DistrictDemandForecastItemResponse> Items,
    int HoursAhead,
    DateTime GeneratedAt);
