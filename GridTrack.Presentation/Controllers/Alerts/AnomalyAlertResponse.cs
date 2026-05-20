using GridTrack.Presentation.Controllers.Shared;

namespace GridTrack.Presentation.Controllers.Alerts;

public sealed record AnomalyAlertResponse(
    string Id,
    string DeliveryId ,
    string DriverId ,
    string DriverName ,
    string AnomalyType ,
    string Reason,
    string DistrictId,
    string DistrictName,
    Coordinate Position,
    DateTime Timestamp
);