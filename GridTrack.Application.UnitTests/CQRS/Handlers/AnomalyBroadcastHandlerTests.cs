using GridTrack.Application.CQRS.Handlers;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

public class AnomalyBroadcastHandlerTests
{
    [Test]
    public async Task Handle_Should_Broadcast_To_Correct_District()
    {
        var push = new FakeDashboardPushService();
        var e = BuildEvent(districtId: "babtouma");

        await AnomalyBroadcastHandler.Handle(e, push, CancellationToken.None);

        await Assert.That(push.AnomalyCalls).Count().IsEqualTo(1);
        await Assert.That(push.AnomalyCalls[0].DistrictId).IsEqualTo("babtouma");
    }

    [Test]
    public async Task Handle_Should_Map_All_Event_Fields_To_Dto()
    {
        var push = new FakeDashboardPushService();
        var deliveryId = Guid.NewGuid();
        var e = new DeliveryFlaggedAnomalousDomainEvent(
            deliveryId, AnomalyType.StalePosition, "No movement for 20 min", "kafrsousa");

        await AnomalyBroadcastHandler.Handle(e, push, CancellationToken.None);

        var dto = push.AnomalyCalls[0].Dto;
        await Assert.That(dto.DeliveryId).IsEqualTo(deliveryId);
        await Assert.That(dto.DistrictId).IsEqualTo("kafrsousa");
        await Assert.That(dto.Type).IsEqualTo(AnomalyType.StalePosition);
        await Assert.That(dto.Reason).IsEqualTo("No movement for 20 min");
    }

    [Test]
    public async Task Handle_Should_Set_Non_Default_Timestamp_On_Dto()
    {
        var push = new FakeDashboardPushService();
        var e = BuildEvent();

        await AnomalyBroadcastHandler.Handle(e, push, CancellationToken.None);

        await Assert.That(push.AnomalyCalls[0].Dto.Timestamp).IsNotEqualTo(default(DateTime));
    }

    [Test]
    [Arguments(AnomalyType.EtaExceeded)]
    [Arguments(AnomalyType.RouteDeviation)]
    [Arguments(AnomalyType.UnexpectedStop)]
    public async Task Handle_Should_Pass_Through_Any_Anomaly_Type(AnomalyType type)
    {
        var push = new FakeDashboardPushService();
        var e = new DeliveryFlaggedAnomalousDomainEvent(Guid.NewGuid(), type, "reason", "mezzeh");

        await AnomalyBroadcastHandler.Handle(e, push, CancellationToken.None);

        await Assert.That(push.AnomalyCalls[0].Dto.Type).IsEqualTo(type);
    }

    private static DeliveryFlaggedAnomalousDomainEvent BuildEvent(string districtId = "mezzeh")
        => new(Guid.NewGuid(), AnomalyType.StalePosition, "stalled", districtId);
}
