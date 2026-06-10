using GridTrack.Domain.ValueObjects;

namespace GridTrack.Application.Dtos;

public sealed record AnomalyTypeCountResponse(AnomalyType AnomalyType, int Count);

public sealed record AnomalyDistrictCountResponse(string DistrictId, int Count);

public sealed record GetAnomalyBreakdownResponse(
    IReadOnlyList<AnomalyTypeCountResponse> ByType,
    IReadOnlyList<AnomalyDistrictCountResponse> ByDistrict);
