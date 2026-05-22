using NetTopologySuite.Geometries;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.ValueObjects;

namespace GridTrack.Domain.Deliveries;

public sealed record DeliveryCreatedDomainEvent(
    Guid DeliveryId,
    DateTime CreatedAt,
    string DistrictId) : IDomainEvent;

public sealed record DeliveryAssignedDomainEvent(
    Guid DeliveryId,
    Guid DriverId) : IDomainEvent;

public sealed record DeliveryPickedUpDomainEvent(
    Guid DeliveryId,
    Point Location,
    DateTime Timestamp) : IDomainEvent;

public sealed record DeliveryLocationUpdatedDomainEvent(
    Guid DeliveryId,
    Point Location,
    DateTime Timestamp) : IDomainEvent;

public sealed record DeliveryCompletedDomainEvent(
    Guid DeliveryId,
    DateTime DeliveredAt,
    Guid? DriverId,
    DateTime? PickedUpAt,
    double ExpectedDurationSeconds) : IDomainEvent;

public sealed record DeliveryFlaggedAnomalousDomainEvent(
    Guid DeliveryId,
    AnomalyType Type,
    string Reason,
    string DistrictId) : IDomainEvent;

public sealed record DeliveryCancelledDomainEvent(
    Guid DeliveryId,
    DateTime CancelledAt,
    string Reason) : IDomainEvent;
