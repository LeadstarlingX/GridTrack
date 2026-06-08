using GridTrack.Application.CQRS.Handlers;
using GridTrack.Application.IntegrationEvents;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

public class ForecastResultHandlerTests
{
    [Test]
    public async Task Handle_Should_Cache_Message_Under_Forecast_Key()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var msg = BuildMessage("mezzeh");

        await ForecastResultHandler.Handle(msg, cache, push, CancellationToken.None);

        await Assert.That(cache.SetCalls).Count().IsEqualTo(1);
        await Assert.That(cache.SetCalls[0].Key).IsEqualTo("forecast:mezzeh");
        await Assert.That(cache.SetCalls[0].Value).IsEqualTo(msg);
    }

    [Test]
    public async Task Handle_Should_Cache_With_Five_Minute_Expiration()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();

        await ForecastResultHandler.Handle(BuildMessage("kafrsousa"), cache, push, CancellationToken.None);

        await Assert.That(cache.SetCalls[0].Expiration).IsEqualTo(TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task Handle_Should_Broadcast_Forecast_Result_To_Push_Service()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var msg = new ForecastResultMessage(
            DistrictId: "babtouma",
            ExpectedDeliveries: 12,
            StaffingRatio: 0.65,
            Label: "Critical",
            Color: "#FF4B4B",
            GeneratedAt: DateTime.UtcNow);

        await ForecastResultHandler.Handle(msg, cache, push, CancellationToken.None);

        await Assert.That(push.ForecastResultCalls).Count().IsEqualTo(1);
        var call = push.ForecastResultCalls[0];
        await Assert.That(call.DistrictId).IsEqualTo("babtouma");
        await Assert.That(call.Expected).IsEqualTo(12);
        await Assert.That(call.Ratio).IsEqualTo(0.65);
        await Assert.That(call.Label).IsEqualTo("Critical");
        await Assert.That(call.Color).IsEqualTo("#FF4B4B");
    }

    [Test]
    public async Task Handle_Should_Use_District_Id_From_Message_As_Cache_Key()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();

        await ForecastResultHandler.Handle(BuildMessage("malki"), cache, push, CancellationToken.None);

        await Assert.That(cache.SetCalls[0].Key).IsEqualTo("forecast:malki");
    }

    [Test]
    public async Task Handle_Should_Cache_And_Broadcast_In_Same_Call()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();

        await ForecastResultHandler.Handle(BuildMessage("mezzeh"), cache, push, CancellationToken.None);

        await Assert.That(cache.SetCalls).Count().IsEqualTo(1);
        await Assert.That(push.ForecastResultCalls).Count().IsEqualTo(1);
    }

    private static ForecastResultMessage BuildMessage(string districtId) => new(
        DistrictId: districtId,
        ExpectedDeliveries: 8,
        StaffingRatio: 0.80,
        Label: "Moderate",
        Color: "#FFA500",
        GeneratedAt: DateTime.UtcNow);
}
