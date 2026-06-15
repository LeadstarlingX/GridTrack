using GridTrack.Application.CQRS.Handlers;
using GridTrack.Application.IntegrationEvents;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

public class DemandSurgeHandlerTests
{
    [Test]
    public async Task Handle_Broadcasts_Surge_With_Correct_Fields()
    {
        var push    = new FakeDashboardPushService();
        var handler = new DemandSurgeHandler();
        var msg     = new DemandSurgeMessage("mezzeh", 42, 10.5, 2.8, DateTime.UtcNow);

        await handler.Handle(msg, push, CancellationToken.None);

        await Assert.That(push.SurgeCalls).Count().IsEqualTo(1);
        var (districtId, count, mean, devs) = push.SurgeCalls[0];
        await Assert.That(districtId).IsEqualTo("mezzeh");
        await Assert.That(count).IsEqualTo(42);
        await Assert.That(mean).IsEqualTo(10.5);
        await Assert.That(devs).IsEqualTo(2.8);
    }

    [Test]
    public async Task Handle_Broadcasts_To_Push_Service_Once()
    {
        var push    = new FakeDashboardPushService();
        var handler = new DemandSurgeHandler();
        var msg     = new DemandSurgeMessage("babtouma", 20, 5.0, 3.1, DateTime.UtcNow);

        await handler.Handle(msg, push, CancellationToken.None);

        await Assert.That(push.SurgeCalls).Count().IsEqualTo(1);
        await Assert.That(push.IncidentCalls).Count().IsEqualTo(0);
    }
}
