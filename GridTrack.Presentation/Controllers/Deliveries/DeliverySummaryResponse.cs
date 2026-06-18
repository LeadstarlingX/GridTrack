using GridTrack.Application.Dtos;

namespace GridTrack.Presentation.Controllers.Deliveries;

public sealed record DeliverySummaryResponse(
    Guid DeliveryId,
    string Status,
    Guid? AssignedDriverId,
    double Lat,
    double Lng,
    string DistrictId,
    DateTime CreatedAt,
    bool AnomalyFlag)
{
    public static DeliverySummaryResponse From(DeliveryDto dto) => new(
        dto.DeliveryId,
        dto.Status.ToString(),
        dto.AssignedDriverId,
        dto.CurrentLocation.Coordinate.Y,
        dto.CurrentLocation.Coordinate.X,
        dto.DistrictId,
        dto.CreatedAt,
        dto.AnomalyFlag);
}
