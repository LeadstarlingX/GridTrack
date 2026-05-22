using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.IntegrationEvents;
using GridTrack.Application.Interfaces;

namespace GridTrack.Application.CQRS.Handlers;

public static class ForecastResultHandler
{
    public static async Task Handle(
        ForecastResultMessage msg,
        ICacheService cache,
        IDashboardPushService push,
        CancellationToken ct)
    {
        await cache.SetAsync(
            $"forecast:{msg.DistrictId}", msg, TimeSpan.FromMinutes(5), ct);

        await push.BroadcastForecastResultAsync(
            msg.DistrictId,
            msg.ExpectedDeliveries,
            msg.StaffingRatio,
            msg.Label,
            msg.Color,
            ct);
    }
}
