using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GridTrack.Infrastructure.Hubs;


internal sealed class DashboardPushService(IHubContext<DashboardHub> hub) : IDashboardPushService
{
    public Task BroadcastDriverPositionAsync(string districtId, DriverDto payload, CancellationToken ct)
        => hub.Clients.Group(districtId).SendCoreAsync(
            "DriverPositionUpdated",
            [new
            {
                driverId   = payload.DriverId,
                lat        = payload.Location.Coordinate.Y,
                lng        = payload.Location.Coordinate.X,
                districtId = payload.DistrictId,
            }],
            ct);

    public Task BroadcastDeliveryUpdateAsync(string districtId, DeliveryDto payload, CancellationToken ct)
        => hub.Clients.Group(districtId).SendCoreAsync(
            "DeliveryUpdated",
            [new
            {
                deliveryId       = payload.DeliveryId,
                status           = payload.Status.ToString(),
                assignedDriverId = payload.AssignedDriverId,
                etaSeconds       = (int?)null,
            }],
            ct);

    public Task BroadcastAnomalyAsync(string districtId, AnomalyAlertDto payload, CancellationToken ct)
        => hub.Clients.Group(districtId).SendCoreAsync(
            "AnomalyBroadcast",
            [new
            {
                deliveryId  = payload.DeliveryId,
                anomalyType = payload.Type.ToString(),
                reason      = payload.Reason,
                districtId  = payload.DistrictId,
                timestamp   = payload.Timestamp,
            }],
            ct);

    public Task BroadcastForecastOverlayAsync(string districtId, ForecastDto payload, CancellationToken ct)
        => hub.Clients.Group(districtId).SendCoreAsync(
            "ForecastOverlayUpdated",
            [new
            {
                districtId       = payload.DistrictId,
                forecastedDemand = payload.ExpectedDeliveries,
                updatedAt        = payload.GeneratedAt,
            }],
            ct);

    public Task BroadcastUrgencyUpdateAsync(Guid deliveryId, string? districtId, int urgencyScore, string aiNote, CancellationToken ct)
    {
        var target = districtId is not null ? hub.Clients.Group(districtId) : hub.Clients.All;
        return target.SendCoreAsync("UrgencyUpdated", [new { deliveryId, urgencyScore, aiNote }], ct);
    }

    public Task BroadcastForecastResultAsync(
        string districtId, int forecastedDemand, DateTime updatedAt, CancellationToken ct)
        => hub.Clients.Group(districtId).SendCoreAsync(
            "ForecastOverlayUpdated",
            [new { districtId, forecastedDemand, updatedAt }],
            ct);

    public Task BroadcastDemandSurgeAsync(
        string districtId, int currentCount, double historicalMean, double deviations, CancellationToken ct)
        => hub.Clients.All.SendCoreAsync(
            "DemandSurge",
            [new { districtId, currentCount, historicalMean, deviations }],
            ct);

    public Task BroadcastAnomalyIncidentAsync(
        string districtId, int anomalyCount, string summary, CancellationToken ct)
        => hub.Clients.All.SendCoreAsync(
            "AnomalyIncident",
            [new { districtId, anomalyCount, summary }],
            ct);
}
