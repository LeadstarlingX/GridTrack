using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GridTrack.Infrastructure.Hubs;

/// <summary>
/// Implements <see cref="IDashboardPushService"/> by broadcasting SignalR messages
/// to district groups on the <see cref="DashboardHub"/>.
/// </summary>
internal sealed class DashboardPushService(IHubContext<DashboardHub> hub) : IDashboardPushService
{
    public Task BroadcastDriverPositionAsync(string districtId, DriverDto payload, CancellationToken ct)
        => hub.Clients.Group(districtId).SendAsync(
            "DriverPositionUpdated",
            new
            {
                driverId = payload.DriverId,
                lat = payload.Location.Y,
                lng = payload.Location.X,
                districtId = payload.DistrictId
            },
            ct);

    public Task BroadcastDeliveryUpdateAsync(string districtId, DeliveryDto payload, CancellationToken ct)
        => hub.Clients.Group(districtId).SendAsync(
            "DeliveryUpdated",
            new
            {
                deliveryId = payload.DeliveryId,
                status = payload.Status.ToString(),
                assignedDriverId = payload.AssignedDriverId,
                etaSeconds = (int?)null   // populated by richer read model when available
            },
            ct);

    public Task BroadcastAnomalyAsync(string districtId, AnomalyAlertDto payload, CancellationToken ct)
        => hub.Clients.Group(districtId).SendAsync(
            "AnomalyBroadcast",
            new
            {
                deliveryId = payload.DeliveryId,
                driverId = payload.DeliveryId,   // AnomalyAlertDto carries DeliveryId; driverId added when model is richer
                anomalyType = payload.Type.ToString(),
                reason = payload.Reason,
                districtId = payload.DistrictId,
                lat = 0.0,
                lng = 0.0,
                timestamp = payload.Timestamp
            },
            ct);

    public Task BroadcastForecastOverlayAsync(string districtId, ForecastDto payload, CancellationToken ct)
        => hub.Clients.Group(districtId).SendAsync(
            "ForecastOverlayUpdated",
            new
            {
                districtId = payload.DistrictId,
                forecastedDemand = payload.ExpectedDeliveries,
                updatedAt = payload.GeneratedAt
            },
            ct);
}
