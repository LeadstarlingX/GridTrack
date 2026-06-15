using GridTrack.Application.CQRS.Handlers;
using GridTrack.Application.IntegrationEvents;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

public class AnomalyIncidentHandlerTests
{
    [Test]
    public async Task Handle_Broadcasts_Incident_With_Correct_Fields()
    {
        var push    = new FakeDashboardPushService();
        var handler = new AnomalyIncidentHandler();
        var msg     = new AnomalyIncidentMessage("mezzeh", 4, 30, "4 stalls detected — check district traffic", DateTime.UtcNow);

        await handler.Handle(msg, push, CancellationToken.None);

        await Assert.That(push.IncidentCalls).Count().IsEqualTo(1);
        var (districtId, count, summary) = push.IncidentCalls[0];
        await Assert.That(districtId).IsEqualTo("mezzeh");
        await Assert.That(count).IsEqualTo(4);
        await Assert.That(summary).IsEqualTo("4 stalls detected — check district traffic");
    }

    [Test]
    public async Task Handle_Does_Not_Trigger_Surge_Broadcast()
    {
        var push    = new FakeDashboardPushService();
        var handler = new AnomalyIncidentHandler();
        var msg     = new AnomalyIncidentMessage("malki", 3, 30, "Alert", DateTime.UtcNow);

        await handler.Handle(msg, push, CancellationToken.None);

        await Assert.That(push.SurgeCalls).Count().IsEqualTo(0);
        await Assert.That(push.IncidentCalls).Count().IsEqualTo(1);
    }
}
