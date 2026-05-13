using GridTrack.Domain.Abstractions;
using GridTrack.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UseCases.Integration;

public sealed record DeliveryCreatedIntegrationEvent(
    Guid DeliveryId,
    string DistrictId,
    DateTime CreatedAt) : IDomainEvent;

public sealed record DeliveryLocationUpdatedIntegrationEvent(
    Guid DeliveryId,
    Point Location,
    DateTime Timestamp) : IDomainEvent;

public sealed record DeliveryFlaggedAnomalousIntegrationEvent(
    Guid DeliveryId,
    AnomalyType Type,
    string Reason) : IDomainEvent;

public sealed record DriverPositionUpdatedIntegrationEvent(
    Guid DriverId,
    Point Location,
    DateTime Timestamp) : IDomainEvent;

public sealed record DriverAvailabilityChangedIntegrationEvent(
    Guid DriverId,
    bool IsActive) : IDomainEvent;
