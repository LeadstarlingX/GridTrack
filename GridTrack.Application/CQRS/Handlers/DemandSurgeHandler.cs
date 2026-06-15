using GridTrack.Application.IntegrationEvents;
using GridTrack.Application.Interfaces;

namespace GridTrack.Application.CQRS.Handlers;

public sealed class DemandSurgeHandler
{
    public async Task Handle(
        DemandSurgeMessage msg,
        IDashboardPushService push,
        CancellationToken ct)
    {
        await push.BroadcastDemandSurgeAsync(
            msg.DistrictId,
            msg.CurrentCount,
            msg.HistoricalMean,
            msg.Deviations,
            ct);
    }
}
