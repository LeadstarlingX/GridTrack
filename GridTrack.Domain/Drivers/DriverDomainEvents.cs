using NetTopologySuite.Geometries;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Domain.Drivers;

public sealed record DriverPositionUpdatedDomainEvent(
    Guid DriverId,
    Point Location,
    DateTime Timestamp,
    string DistrictId) : IDomainEvent;

public sealed record DriverAvailabilityChangedDomainEvent(
    Guid DriverId,
    bool IsActive) : IDomainEvent;

public sealed record DriverEnteredDistrictDomainEvent(
    Guid DriverId,
    string DistrictId) : IDomainEvent;
