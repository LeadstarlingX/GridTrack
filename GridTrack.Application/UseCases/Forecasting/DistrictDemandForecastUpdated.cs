using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Forecasting;

public sealed record DistrictDemandForecastUpdated(
    string DistrictId,
    DateTime ForecastWindow,
    DateTime GeneratedAt) : IDomainEvent;
