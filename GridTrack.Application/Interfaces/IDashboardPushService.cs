using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IDashboardPushService
{
    Task BroadcastDriverPositionAsync(string districtId, DriverDto payload, CancellationToken ct);
    Task BroadcastDeliveryUpdateAsync(string districtId, DeliveryDto payload, CancellationToken ct);
    Task BroadcastAnomalyAsync(string districtId, AnomalyAlertDto payload, CancellationToken ct);
    Task BroadcastForecastOverlayAsync(string districtId, ForecastDto payload, CancellationToken ct);

    // Called by inbound Python result handlers
    Task BroadcastUrgencyUpdateAsync(Guid deliveryId, string? districtId, int urgencyScore, string aiNote, CancellationToken ct);
    Task BroadcastForecastResultAsync(string districtId, int forecastedDemand, DateTime updatedAt, CancellationToken ct);
}
