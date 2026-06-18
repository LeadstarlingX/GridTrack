namespace GridTrack.Presentation.Controllers.Telemetry;

public sealed record TelemetryPositionRequest(
    Guid DriverId,
    double Lat,
    double Lng,
    DateTime? Timestamp);
