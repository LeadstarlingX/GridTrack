namespace GridTrack.Presentation.Controllers.Telemetry;

public sealed record TelemetryBatchRequest(List<TelemetryEventRequest> Events);

/// <summary>
/// A single telemetry event from an external source (mobile app, simulator, IoT device).
/// </summary>
/// <param name="Type">position | delivery_created | delivery_status</param>
/// <param name="OccurredAt">Event timestamp (UTC). Defaults to server time if omitted.</param>
/// <param name="DriverId">Required for: position, delivery_created (optional)</param>
/// <param name="DeliveryId">Required for: delivery_status; optional new ID for delivery_created</param>
/// <param name="Lat">WGS-84 latitude. Required for: position, delivery_created, delivery_status/picked_up</param>
/// <param name="Lng">WGS-84 longitude. Required for: position, delivery_created, delivery_status/picked_up</param>
/// <param name="Status">For delivery_status: picked_up | delivered | cancelled</param>
/// <param name="CancelReason">For delivery_status/cancelled: human-readable reason</param>
/// <param name="ExpectedEta">For delivery_created: optional delivery deadline (UTC)</param>
/// <param name="DistrictId">For delivery_created: optional override; resolved from H3 if omitted</param>
public sealed record TelemetryEventRequest(
    string? Type,
    DateTime? OccurredAt,
    Guid? DriverId,
    Guid? DeliveryId,
    double? Lat,
    double? Lng,
    string? Status,
    string? CancelReason,
    DateTime? ExpectedEta,
    string? DistrictId);
