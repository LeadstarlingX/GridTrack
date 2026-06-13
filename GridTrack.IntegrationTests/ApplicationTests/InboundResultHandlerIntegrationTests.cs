using Dapper;
using FluentAssertions;
using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.IntegrationEvents;
using GridTrack.Domain.Deliveries;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
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

    [Test]
    [NotInParallel(Order = 612)]
    public async Task UrgencyResult_Should_Persist_Score_To_Delivery()
    {
        await ResetDatabaseAsync();

        var geoFactory = new GeometryFactory(new PrecisionModel(), 4326);
        var location = geoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var delivery = Delivery.Create(Guid.NewGuid(), location, "h3-urgency", DateTime.UtcNow, null).Value;
        delivery.ClearDomainEvents();
        await SeedDeliveriesAsync([delivery]);

        var msg = new UrgencyResultMessage(delivery.DeliveryId, 8, "Urgency note");
        await InvokeAsync(msg);

        await using var scope = Factory.Services.CreateAsyncScope();
        var sql = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        using var conn = sql.CreateConnection();

        var row = await conn.QueryFirstAsync<(int? Score, DateTime? ScoredAt)>(
            """SELECT "UrgencyScore", "UrgencyScoreAt" FROM public."Deliveries" WHERE "DeliveryId" = @Id""",
            new { Id = delivery.DeliveryId });

        row.Score.Should().Be(8);
        row.ScoredAt.Should().NotBeNull();
    }
}
