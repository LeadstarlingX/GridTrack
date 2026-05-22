namespace GridTrack.Application.Dtos;

public sealed record AnomalyAlertItemResponse(
    Guid Id,
    Guid DeliveryId,
    Guid? DriverId,
    string DriverName,
    string AnomalyType,
    string Reason,
    string DistrictId,
    string DistrictName,
    double Lat,
    double Lng,
    DateTime Timestamp);

public sealed record GetAlertsResponse(
    IReadOnlyList<AnomalyAlertItemResponse> Items,
    string? NextCursor);
