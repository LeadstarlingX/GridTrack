namespace GridTrack.Application.Dtos;

public sealed record ForecastDto(
    string DistrictId,
    DateTime ForecastWindow,
    int ExpectedDeliveries,
    DateTime GeneratedAt);
