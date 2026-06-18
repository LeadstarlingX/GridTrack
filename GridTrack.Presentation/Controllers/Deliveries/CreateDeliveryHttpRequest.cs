namespace GridTrack.Presentation.Controllers.Deliveries;

public sealed record CreateDeliveryHttpRequest(
    double Lat,
    double Lng,
    string? DistrictId,
    DateTime? ExpectedEta);
