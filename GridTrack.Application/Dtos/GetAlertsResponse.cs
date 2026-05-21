namespace GridTrack.Application.Dtos;

public sealed record AnomalyAlertItemResponse(
    string Id,
    string DeliveryId,
    string DriverId,
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
