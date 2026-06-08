using GridTrack.Application.CQRS.Handlers;
using GridTrack.Application.IntegrationEvents;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

public class UrgencyResultHandlerTests
{
    [Test]
    public async Task Handle_Should_Cache_Message_Under_Urgency_Key()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var deliveryId = Guid.NewGuid();
        var msg = new UrgencyResultMessage(deliveryId, UrgencyScore: 8, AiNote: "Driver is significantly delayed.");

        await UrgencyResultHandler.Handle(msg, cache, push, CancellationToken.None);

        await Assert.That(cache.SetCalls).Count().IsEqualTo(1);
        await Assert.That(cache.SetCalls[0].Key).IsEqualTo($"urgency:{deliveryId}");
        await Assert.That(cache.SetCalls[0].Value).IsEqualTo(msg);
    }

    [Test]
    public async Task Handle_Should_Cache_With_Ten_Minute_Expiration()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var msg = new UrgencyResultMessage(Guid.NewGuid(), 5, "Moderate delay.");

        await UrgencyResultHandler.Handle(msg, cache, push, CancellationToken.None);

        await Assert.That(cache.SetCalls[0].Expiration).IsEqualTo(TimeSpan.FromMinutes(10));
    }

    [Test]
    public async Task Handle_Should_Broadcast_Urgency_To_Push_Service()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var deliveryId = Guid.NewGuid();
        var msg = new UrgencyResultMessage(deliveryId, UrgencyScore: 9, AiNote: "Critical eta breach.");

        await UrgencyResultHandler.Handle(msg, cache, push, CancellationToken.None);

        await Assert.That(push.UrgencyCalls).Count().IsEqualTo(1);
        await Assert.That(push.UrgencyCalls[0].DeliveryId).IsEqualTo(deliveryId);
        await Assert.That(push.UrgencyCalls[0].Score).IsEqualTo(9);
        await Assert.That(push.UrgencyCalls[0].Note).IsEqualTo("Critical eta breach.");
    }

    [Test]
    public async Task Handle_Should_Cache_And_Broadcast_In_Same_Call()
    {
        // Verifies both side-effects fire — not one or the other
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var msg = new UrgencyResultMessage(Guid.NewGuid(), 3, "Minor delay.");

        await UrgencyResultHandler.Handle(msg, cache, push, CancellationToken.None);

        await Assert.That(cache.SetCalls).Count().IsEqualTo(1);
        await Assert.That(push.UrgencyCalls).Count().IsEqualTo(1);
    }
}
