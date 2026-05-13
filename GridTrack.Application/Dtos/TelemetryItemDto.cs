using NetTopologySuite.Geometries;

namespace GridTrack.Application.Dtos;

public sealed record TelemetryItemDto(
    Guid DriverId,
    Guid? DeliveryId,
    Point Location,
    DateTime Timestamp,
    string? DistrictId);
