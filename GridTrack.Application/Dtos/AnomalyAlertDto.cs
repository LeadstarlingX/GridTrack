using GridTrack.Domain.ValueObjects;

namespace GridTrack.Application.Dtos;

public sealed class AnomalyAlertDto
{
    public Guid DeliveryId { get; init; }
    public string DistrictId { get; init; } = string.Empty;
    public AnomalyType Type { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
