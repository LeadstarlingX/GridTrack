using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Forecast;

public sealed record DistrictDemandForecastUpdated(
    string DistrictId,
    DateTime ForecastWindow,
    DateTime GeneratedAt) : IDomainEvent;
