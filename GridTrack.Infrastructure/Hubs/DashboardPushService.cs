using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GridTrack.Infrastructure.Hubs;


internal sealed class DashboardPushService(
    IHubContext<DashboardHub> hub,
    IDistrictGroupCache districtGroupCache) : IDashboardPushService
{
    public async Task BroadcastDriverPositionAsync(string districtId, DriverDto payload, CancellationToken ct)
    {
        var message = new
        {
            driverId   = payload.DriverId,
            lat        = payload.Location.Coordinate.Y,
            lng        = payload.Location.Coordinate.X,
            districtId = payload.DistrictId,
        };

        // Broadcast to subscribers of this specific district.
        await hub.Clients.Group(districtId).SendCoreAsync("DriverPositionUpdated", [message], ct);

        // Fan-out to any district groups that include this district.
        var groupIds = await districtGroupCache.GetGroupIdsForDistrictAsync(districtId, ct);
        foreach (var groupId in groupIds)
            await hub.Clients.Group($"dg:{groupId}").SendCoreAsync("DriverPositionUpdated", [message], ct);
    }

    public async Task BroadcastDeliveryUpdateAsync(string districtId, DeliveryDto payload, CancellationToken ct)
    {
        Console.WriteLine($"[PUSH] Broadcasting DeliveryUpdated id={payload.DeliveryId} etaSecs={(payload.ExpectedEta.HasValue
            ? (int)Math.Max(0, (payload.ExpectedEta.Value - DateTime.UtcNow).TotalSeconds) : -1)}");
        await hub.Clients.All.SendCoreAsync(
            "DeliveryUpdated",
            [
                new
                {
                    deliveryId = payload.DeliveryId,
                    status = payload.Status.ToString(),
                    assignedDriverId = payload.AssignedDriverId,
                    etaSeconds = payload.ExpectedEta.HasValue
                        ? (int)Math.Max(0, (payload.ExpectedEta.Value - DateTime.UtcNow).TotalSeconds)
                        : (int?)null,
                }
            ],
            ct);

    }

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
            [new { districtId, currentCount, historicalMean, deviations, detectedAt = DateTime.UtcNow }],
            ct);

    public Task BroadcastAnomalyIncidentAsync(
        string districtId, int anomalyCount, string summary, CancellationToken ct)
        => hub.Clients.All.SendCoreAsync(
            "AnomalyIncident",
            [new { districtId, anomalyCount, windowMinutes = 30, summary, detectedAt = DateTime.UtcNow }],
            ct);
}
