using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

internal sealed class FakeDashboardPushService : IDashboardPushService
{
    public List<(string DistrictId, DriverDto Dto)> DriverCalls { get; } = new();
    public List<(string DistrictId, DeliveryDto Dto)> DeliveryCalls { get; } = new();
    public List<(string DistrictId, AnomalyAlertDto Dto)> AnomalyCalls { get; } = new();
    public List<(string DistrictId, ForecastDto Dto)> ForecastOverlayCalls { get; } = new();
    public List<(Guid DeliveryId, string? DistrictId, int Score, string Note)> UrgencyCalls { get; } = new();
    public List<(string DistrictId, int ForecastedDemand, DateTime UpdatedAt)> ForecastResultCalls { get; } = new();

    public Task BroadcastDriverPositionAsync(string districtId, DriverDto payload, CancellationToken ct)
    {
        DriverCalls.Add((districtId, payload));
        return Task.CompletedTask;
    }

    public Task BroadcastDeliveryUpdateAsync(string districtId, DeliveryDto payload, CancellationToken ct)
    {
        DeliveryCalls.Add((districtId, payload));
        return Task.CompletedTask;
    }

    public Task BroadcastAnomalyAsync(string districtId, AnomalyAlertDto payload, CancellationToken ct)
    {
        AnomalyCalls.Add((districtId, payload));
        return Task.CompletedTask;
    }

    public Task BroadcastForecastOverlayAsync(string districtId, ForecastDto payload, CancellationToken ct)
    {
        ForecastOverlayCalls.Add((districtId, payload));
        return Task.CompletedTask;
    }

    public Task BroadcastUrgencyUpdateAsync(Guid deliveryId, string? districtId, int urgencyScore, string aiNote, CancellationToken ct)
    {
        UrgencyCalls.Add((deliveryId, districtId, urgencyScore, aiNote));
        return Task.CompletedTask;
    }

    public Task BroadcastForecastResultAsync(string districtId, int forecastedDemand, DateTime updatedAt, CancellationToken ct)
    {
        ForecastResultCalls.Add((districtId, forecastedDemand, updatedAt));
        return Task.CompletedTask;
    }
}
