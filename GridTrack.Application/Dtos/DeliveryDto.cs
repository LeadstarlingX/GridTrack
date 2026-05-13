using GridTrack.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.Dtos;

public sealed record DeliveryDto(
    Guid DeliveryId,
    Point CurrentLocation,
    DeliveryStatus Status,
    Guid? AssignedDriverId,
    DateTime? ExpectedEta,
    DateTime? ActualEta,
    string DistrictId,
    bool AnomalyFlag,
    DateTime CreatedAt,
    DateTime? PickedUpAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? AnomalyReason);
