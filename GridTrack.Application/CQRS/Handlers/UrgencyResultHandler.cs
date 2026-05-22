using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.IntegrationEvents;
using GridTrack.Application.Interfaces;

namespace GridTrack.Application.CQRS.Handlers;

public static class UrgencyResultHandler
{
    public static async Task Handle(
        UrgencyResultMessage msg,
        ICacheService cache,
        IDashboardPushService push,
        CancellationToken ct)
    {
        await cache.SetAsync(
            $"urgency:{msg.DeliveryId}", msg, TimeSpan.FromMinutes(10), ct);

        await push.BroadcastUrgencyUpdateAsync(
            msg.DeliveryId, msg.UrgencyScore, msg.AiNote, ct);
    }
}
