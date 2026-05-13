using NetTopologySuite.Geometries;

namespace GridTrack.Application.Dtos;

public sealed record DriverDto(
    Guid DriverId,
    Point Location,
    bool IsActive,
    DateTime LastSeen,
    string DistrictId);
