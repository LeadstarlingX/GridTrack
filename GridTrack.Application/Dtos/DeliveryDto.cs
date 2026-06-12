using GridTrack.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.Dtos;

public sealed class DeliveryDto
{
    public Guid DeliveryId { get; init; }
    public Geometry CurrentLocation { get; init; } = null!;
    public DeliveryStatus Status { get; init; }
    public Guid? AssignedDriverId { get; init; }
    public DateTime? ExpectedEta { get; init; }
    public DateTime? ActualEta { get; init; }
    public string DistrictId { get; init; } = string.Empty;
    public bool AnomalyFlag { get; init; }
    public AnomalyType? AnomalyTypeValue { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PickedUpAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? AnomalyReason { get; init; }
}
