using FluentAssertions;
using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.IntegrationEvents;
using GridTrack.IntegrationTests.Abstractions;
using NSubstitute;

namespace GridTrack.IntegrationTests.ApplicationTests;

/// <summary>Tests UrgencyResultHandler / ForecastResultHandler in-process — no RabbitMQ or Python required.</summary>
public class InboundResultHandlerIntegrationTests : BaseIntegrationTest
{
    [Test]
    [NotInParallel(Order = 610)]
    public async Task UrgencyResult_Should_Be_Cached_And_Broadcast()
    {
        Factory.DashboardPushMock.ClearReceivedCalls();
        var deliveryId = Guid.NewGuid();
        var msg = new UrgencyResultMessage(deliveryId, 7, "Check driver now");

        await InvokeAsync(msg);

        var cached = await ResolveAsync<ICacheService, UrgencyResultMessage?>(
            cache => cache.GetAsync<UrgencyResultMessage>($"urgency:{deliveryId}"));

        cached.Should().NotBeNull();
        cached!.UrgencyScore.Should().Be(7);
        cached.AiNote.Should().Be("Check driver now");

        await Factory.DashboardPushMock
            .Received()
            .BroadcastUrgencyUpdateAsync(deliveryId, 7, "Check driver now", Arg.Any<CancellationToken>());
    }

    [Test]
    [NotInParallel(Order = 611)]
    public async Task ForecastResult_Should_Be_Cached_And_Broadcast()
    {
        Factory.DashboardPushMock.ClearReceivedCalls();
        var msg = new ForecastResultMessage("mezzeh", 12, 0.5, "Critical", "#f87171", DateTime.UtcNow);

        await InvokeAsync(msg);

        var cached = await ResolveAsync<ICacheService, ForecastResultMessage?>(
            cache => cache.GetAsync<ForecastResultMessage>("forecast:mezzeh"));

        cached.Should().NotBeNull();
        cached!.ExpectedDeliveries.Should().Be(12);
        cached.Label.Should().Be("Critical");

        await Factory.DashboardPushMock
            .Received()
            .BroadcastForecastResultAsync("mezzeh", 12, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }
}
