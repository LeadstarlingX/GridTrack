using GridTrack.Domain.ValueObjects;

namespace GridTrack.Application.Dtos;

public sealed record AnomalyAlertDto(
    Guid DeliveryId,
    string DistrictId,
    AnomalyType Type,
    string Reason,
    DateTime Timestamp);
