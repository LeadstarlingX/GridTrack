namespace GridTrack.Application.IntegrationEvents;

/// <summary>Outbound: .NET → Python. Fired when a delivery is flagged anomalous.</summary>
public record DeliveryAnomalyIntegrationEvent(
    Guid     DeliveryId,
    string   DistrictId,
    string   AnomalyType,
    string   Reason,
    double   DriverLat,
    double   DriverLng,
    DateTime OccurredAt);

/// <summary>Outbound: .NET → Python. Fired on every driver position update.</summary>
public record DriverPositionIntegrationEvent(
    Guid     DriverId,
    string   DistrictId,
    double   Lat,
    double   Lng,
    string   DeliveryStatus,
    DateTime Timestamp);

/// <summary>Outbound: .NET → Python. Fired when a delivery is marked Delivered.</summary>
public record DeliveryCompletedIntegrationEvent(
    Guid     DeliveryId,
    Guid     DriverId,
    string   DistrictId,
    DateTime PickedUpAt,
    DateTime DeliveredAt,
    double   ActualDurationSeconds,
    double   ExpectedDurationSeconds);

/// <summary>Inbound: Python → .NET. Urgency score + AI note for a flagged delivery.</summary>
public record UrgencyResultMessage(
    Guid   DeliveryId,
    int    UrgencyScore,
    string AiNote);

/// <summary>Inbound: Python → .NET. Demand forecast for a district.</summary>
public record ForecastResultMessage(
    string   DistrictId,
    int      ExpectedDeliveries,
    double   StaffingRatio,
    string   Label,
    string   Color,
    DateTime GeneratedAt);
