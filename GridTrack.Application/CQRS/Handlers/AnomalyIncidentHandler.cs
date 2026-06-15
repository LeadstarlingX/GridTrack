using GridTrack.Application.IntegrationEvents;
using GridTrack.Application.Interfaces;

namespace GridTrack.Application.CQRS.Handlers;

public sealed class AnomalyIncidentHandler
{
    public async Task Handle(
        AnomalyIncidentMessage msg,
        IDashboardPushService push,
        CancellationToken ct)
    {
        await push.BroadcastAnomalyIncidentAsync(
            msg.DistrictId,
            msg.AnomalyCount,
            msg.Summary,
            ct);
    }
}
