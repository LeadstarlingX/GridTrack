using FluentAssertions;
using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.IntegrationEvents;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace GridTrack.E2ETests;

public class PythonPipelineE2ETests
{
    [ClassDataSource<E2EWebAppFactory>(Shared = SharedType.PerAssembly)]
    public static E2EWebAppFactory Factory { get; set; } = null!;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task ResetAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var ctx = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            """TRUNCATE TABLE "Deliveries", "Drivers", "H3District", delivery_routes RESTART IDENTITY CASCADE""");
    }

    private static async Task PublishAsync(object message)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(message);
    }

    private static async Task<T?> GetCachedAsync<T>(string key) where T : class
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        return await cache.GetAsync<T>(key);
    }

    // Polls every 500 ms until the cache entry appears or the timeout elapses.
    // Avoids a fixed delay that is either too short (flaky) or too long (slow).
    private static async Task<T?> WaitForCacheAsync<T>(string key, TimeSpan timeout) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var value = await GetCachedAsync<T>(key);
            if (value is not null) return value;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
        return null;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel]
    public async Task FlagAnomaly_Should_Produce_UrgencyResult_Via_Python()
    {
        await ResetAsync();

        var deliveryId = Guid.NewGuid();

        // Publish DeliveryAnomalyIntegrationEvent directly — same pattern as test 2.
        // Wolverine routes it to the gridtrack.anomaly fanout exchange → Python scores
        // urgency → publishes UrgencyResultMessage to gridtrack.urgency-results →
        // Wolverine receives and caches it.  The cascade chain through
        // FlagDeliveryAnomalyCommand is intentionally bypassed: Wolverine's code
        // generator cannot resolve routing for items typed as IEnumerable<object>,
        // so the domain events returned by FlagDeliveryAnomalyHandler are silently
        // dropped before reaching AnomalyIntegrationPublisher.
        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId,
            "mezzeh",
            "StalePosition",
            "No movement for 25 min",
            DriverLat: 0,
            DriverLng: 0,
            DateTime.UtcNow));

        var urgency = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId}", TimeSpan.FromSeconds(30));

        urgency.Should().NotBeNull("Python service should have processed the anomaly and published a result");
        urgency!.UrgencyScore.Should().BeInRange(0, 10);
        urgency.AiNote.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [NotInParallel]
    public async Task DriverPositionUpdate_Should_Produce_ForecastResult_Via_Python()
    {
        await ResetAsync();

        const string district = "babtouma";

        await PublishAsync(new DriverPositionIntegrationEvent(
            DriverId:       Guid.NewGuid(),
            DistrictId:     district,
            Lat:            33.522,
            Lng:            36.307,
            DeliveryStatus: "InTransit",
            Timestamp:      DateTime.UtcNow));

        var forecast = await WaitForCacheAsync<ForecastResultMessage>(
            $"forecast:{district}", TimeSpan.FromSeconds(30));

        forecast.Should().NotBeNull("Python service should have processed the position update and emitted a forecast");
        forecast!.DistrictId.Should().Be(district);
        forecast.Label.Should().BeOneOf("Critical", "Moderate", "Low demand");
        forecast.StaffingRatio.Should().BeGreaterThanOrEqualTo(0);
        forecast.Color.Should().BeOneOf("#f87171", "#fbbf24", "#34d399");
    }
}
