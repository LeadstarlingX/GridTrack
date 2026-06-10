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

    // ── District usage for position updates (one district per test) ───────────
    //
    // Python's update_forecast() has a module-level _last_emit dict that gates
    // forecasts to once per EMIT_INTERVAL (5 min) per district — state persists
    // across all tests within the same Python container process.  To prevent a
    // second position-update test for the same district from getting no result
    // (update_forecast returns None if district was emitted <5 min ago), each
    // test below uses a unique district for position-update events:
    //
    //   "babtouma"  → DriverPositionUpdate_Should_Produce_ForecastResult_Via_Python
    //   "mezzeh"    → TwoPositionUpdates_ForDifferentDistricts
    //   "qaboun"    → TwoPositionUpdates_ForDifferentDistricts
    //   "malki"     → SinglePositionUpdate_Should_Produce_Deterministic_CriticalForecast
    //   "kafrsousa" → AnomalyAndPositionUpdate_PublishedTogether
    //
    // Anomaly-only tests are free to reuse any district string because
    // score_anomaly() is stateless.

    // ── Anomaly → Urgency pipeline ────────────────────────────────────────────

    [Test]
    [NotInParallel]
    public async Task StalePosition_Anomaly_Should_Produce_UrgencyResult_Via_Python()
    {
        await ResetAsync();

        var deliveryId = Guid.NewGuid();

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
    public async Task RouteDeviation_Anomaly_Should_Produce_UrgencyResult_Via_Python()
    {
        // Python URGENCY_BASE assigns score 4 to RouteDeviation.
        await ResetAsync();

        var deliveryId = Guid.NewGuid();

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId,
            "qaboun",
            "RouteDeviation",
            "Driver is 800 m off planned route",
            DriverLat: 33.543,
            DriverLng: 36.298,
            DateTime.UtcNow));

        var urgency = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId}", TimeSpan.FromSeconds(30));

        urgency.Should().NotBeNull("Python service should score RouteDeviation anomalies");
        urgency!.UrgencyScore.Should().BeInRange(0, 10);
        urgency.AiNote.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [NotInParallel]
    public async Task UrgencyResult_Should_EchoBack_CorrectDeliveryId()
    {
        // Verifies the deliveryId round-trip: Python must echo the exact Guid it
        // received so that .NET can route the urgency update to the right delivery.
        await ResetAsync();

        var deliveryId = Guid.NewGuid();

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId,
            "malki",
            "EtaExceeded",
            "Expected delivery time exceeded by 40 min",
            DriverLat: 0,
            DriverLng: 0,
            DateTime.UtcNow));

        var urgency = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId}", TimeSpan.FromSeconds(30));

        urgency.Should().NotBeNull();
        urgency!.DeliveryId.Should().Be(deliveryId,
            "Python must echo the exact deliveryId from the inbound event");
        urgency.UrgencyScore.Should().BeInRange(0, 10);
        urgency.AiNote.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [NotInParallel]
    public async Task TwoAnomalies_ForDifferentDeliveries_Should_ProduceIndependentUrgencyResults()
    {
        // Verifies cache key isolation: each delivery ID gets its own urgency entry.
        // Both events are published before polling so Python processes them in
        // parallel within the shared 30-second window.
        await ResetAsync();

        var deliveryId1 = Guid.NewGuid();
        var deliveryId2 = Guid.NewGuid();

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId1, "mezzeh", "StalePosition", "No movement for 25 min",
            DriverLat: 0, DriverLng: 0, DateTime.UtcNow));

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId2, "babtouma", "UnexpectedStop", "Driver stopped for 15 min mid-route",
            DriverLat: 0, DriverLng: 0, DateTime.UtcNow));

        var urgency1 = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId1}", TimeSpan.FromSeconds(30));
        var urgency2 = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId2}", TimeSpan.FromSeconds(30));

        urgency1.Should().NotBeNull("first delivery should have an independent urgency result");
        urgency2.Should().NotBeNull("second delivery should have an independent urgency result");
        urgency1!.DeliveryId.Should().Be(deliveryId1);
        urgency2!.DeliveryId.Should().Be(deliveryId2);
        urgency1.UrgencyScore.Should().BeInRange(0, 10);
        urgency2.UrgencyScore.Should().BeInRange(0, 10);
    }

    // ── Exact scoring verification ─────────────────────────────────────────────
    //
    // Python's scoring is deterministic: score = URGENCY_BASE[type] + DISTRICT_BOOST[district].
    // URGENCY_BASE:   StalePosition=5, EtaExceeded=3, RouteDeviation=4, UnexpectedStop=4, unknown=2
    // DISTRICT_BOOST: kafrsousa=2, babtouma=1, malki=0, mezzeh=0, unknown=0

    [Test]
    [NotInParallel]
    public async Task StalePosition_InKafrsousa_Should_Produce_Score7()
    {
        // kafrsousa has the highest district boost (+2).
        // StalePosition (5) + kafrsousa (2) = 7 — the highest achievable score
        // with current Python configuration without hitting the cap of 10.
        await ResetAsync();

        var deliveryId = Guid.NewGuid();

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId,
            "kafrsousa",
            "StalePosition",
            "No GPS ping for 30 min",
            DriverLat: 0,
            DriverLng: 0,
            DateTime.UtcNow));

        var urgency = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId}", TimeSpan.FromSeconds(30));

        urgency.Should().NotBeNull();
        urgency!.UrgencyScore.Should().Be(7,
            "StalePosition base score 5 + kafrsousa district boost 2 = 7");
        urgency.AiNote.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [NotInParallel]
    public async Task EtaExceeded_InBabtouma_Should_Produce_Score4()
    {
        // EtaExceeded (3) + babtouma (1) = 4. Verifies the boost is additive.
        await ResetAsync();

        var deliveryId = Guid.NewGuid();

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId,
            "babtouma",
            "EtaExceeded",
            "Expected delivery time exceeded by 40 min",
            DriverLat: 0,
            DriverLng: 0,
            DateTime.UtcNow));

        var urgency = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId}", TimeSpan.FromSeconds(30));

        urgency.Should().NotBeNull();
        urgency!.UrgencyScore.Should().Be(4,
            "EtaExceeded base score 3 + babtouma district boost 1 = 4");
        urgency.AiNote.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [NotInParallel]
    public async Task UnknownAnomalyType_Should_Produce_Score2_WithDefaultFallback()
    {
        // "NetworkIssue" is not in Python's URGENCY_BASE.
        // Python falls back to the default score of 2: URGENCY_BASE.get(type, 2).
        // This verifies the service handles unexpected anomaly types gracefully
        // rather than crashing or producing a null result.
        await ResetAsync();

        var deliveryId = Guid.NewGuid();

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId,
            "mezzeh",             // mezzeh boost = 0 → score stays at 2
            "NetworkIssue",       // not in URGENCY_BASE → default 2
            "Driver app lost connection to server",
            DriverLat: 0,
            DriverLng: 0,
            DateTime.UtcNow));

        var urgency = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId}", TimeSpan.FromSeconds(30));

        urgency.Should().NotBeNull("Python must handle unrecognised anomaly types with a default score, not a crash");
        urgency!.UrgencyScore.Should().Be(2,
            "unknown anomaly type falls back to default score 2 in Python's URGENCY_BASE.get(type, 2)");
        urgency.AiNote.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [NotInParallel]
    public async Task AnomalyWithArabicReason_Should_Still_Produce_UrgencyResult()
    {
        // Verifies UTF-8 handling through the entire pipeline:
        //   System.Text.Json (encodes Arabic to UTF-8 JSON)
        //   → RabbitMQ (transmits as raw bytes)
        //   → aio_pika (delivers msg.body as bytes)
        //   → Pydantic model_validate_json (decodes UTF-8)
        //   → Python _fallback_note calls .lower() on Arabic text (no-op, safe)
        //   → UrgencyResultMessage published back
        // Relevant for Damascus operators who enter anomaly reasons in Arabic.
        await ResetAsync();

        var deliveryId = Guid.NewGuid();

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId,
            "mezzeh",
            "StalePosition",
            "السائق لم يتحرك منذ ٢٥ دقيقة",  // "The driver has not moved for 25 minutes"
            DriverLat: 33.505,
            DriverLng: 36.243,
            DateTime.UtcNow));

        var urgency = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId}", TimeSpan.FromSeconds(30));

        urgency.Should().NotBeNull("Arabic text in the reason field must not break serialisation or Python parsing");
        urgency!.UrgencyScore.Should().Be(5, "StalePosition (5) + mezzeh boost (0) = 5");
        urgency.AiNote.Should().NotBeNullOrWhiteSpace();
    }

    // ── Position → Forecast pipeline ─────────────────────────────────────────

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

    [Test]
    [NotInParallel]
    public async Task TwoPositionUpdates_ForDifferentDistricts_Should_ProduceIndependentForecasts()
    {
        // Verifies forecast cache entries are keyed by districtId and do not
        // overwrite each other.  Both events are published before polling so
        // Python processes them concurrently within the shared 30-second window.
        await ResetAsync();

        await PublishAsync(new DriverPositionIntegrationEvent(
            DriverId:       Guid.NewGuid(),
            DistrictId:     "mezzeh",
            Lat:            33.505,
            Lng:            36.243,
            DeliveryStatus: "InTransit",
            Timestamp:      DateTime.UtcNow));

        await PublishAsync(new DriverPositionIntegrationEvent(
            DriverId:       Guid.NewGuid(),
            DistrictId:     "qaboun",
            Lat:            33.543,
            Lng:            36.298,
            DeliveryStatus: "InTransit",
            Timestamp:      DateTime.UtcNow));

        var mezzehForecast = await WaitForCacheAsync<ForecastResultMessage>(
            "forecast:mezzeh", TimeSpan.FromSeconds(30));
        var qabounForecast = await WaitForCacheAsync<ForecastResultMessage>(
            "forecast:qaboun", TimeSpan.FromSeconds(30));

        mezzehForecast.Should().NotBeNull("mezzeh should have its own forecast");
        qabounForecast.Should().NotBeNull("qaboun should have its own forecast");
        mezzehForecast!.DistrictId.Should().Be("mezzeh");
        qabounForecast!.DistrictId.Should().Be("qaboun");
        mezzehForecast.Label.Should().BeOneOf("Critical", "Moderate", "Low demand");
        qabounForecast.Label.Should().BeOneOf("Critical", "Moderate", "Low demand");
        mezzehForecast.Color.Should().BeOneOf("#f87171", "#fbbf24", "#34d399");
        qabounForecast.Color.Should().BeOneOf("#f87171", "#fbbf24", "#34d399");
    }

    [Test]
    [NotInParallel]
    public async Task SinglePositionUpdate_Should_Produce_Deterministic_CriticalForecast()
    {
        // For a brand-new district with exactly one position event, Python's
        // update_forecast() produces fully deterministic output:
        //
        //   _windows["malki"] = [now]          → count_last_30 = 1
        //   expectedDeliveries = 1 * 2 = 2
        //   _active_drivers["malki"] = {driver} → driver_count = 1
        //   staffingRatio = 1 / 2 = 0.50
        //   0.50 < CRITICAL_RATIO (0.70)       → label = "Critical", color = "#f87171"
        //
        // This test pins the exact forecast values so a regression in Python's
        // windowing or ratio logic is caught immediately at the E2E level.
        await ResetAsync();

        await PublishAsync(new DriverPositionIntegrationEvent(
            DriverId:       Guid.NewGuid(),
            DistrictId:     "malki",
            Lat:            33.511,
            Lng:            36.281,
            DeliveryStatus: "InTransit",
            Timestamp:      DateTime.UtcNow));

        var forecast = await WaitForCacheAsync<ForecastResultMessage>(
            "forecast:malki", TimeSpan.FromSeconds(30));

        forecast.Should().NotBeNull();
        forecast!.DistrictId.Should().Be("malki");
        forecast.ExpectedDeliveries.Should().Be(2,
            "one event in the last 30 min → expectedDeliveries = 1 * 2 = 2");
        forecast.StaffingRatio.Should().BeApproximately(0.5, precision: 0.01,
            "1 active driver / 2 expected deliveries = 0.50 staffing ratio");
        forecast.Label.Should().Be("Critical",
            "staffing ratio 0.50 is below CRITICAL_RATIO (0.70)");
        forecast.Color.Should().Be("#f87171");
        forecast.GeneratedAt.Should().BeAfter(DateTime.UnixEpoch,
            "GeneratedAt must be a real UTC timestamp, not the default DateTime value");
    }

    [Test]
    [NotInParallel]
    public async Task AnomalyAndPositionUpdate_PublishedTogether_Should_Both_Produce_Results()
    {
        // Fires one event into each exchange simultaneously.  Python's consumer
        // subscribes to gridtrack.anomaly and gridtrack.positions independently via
        // two separate queue bindings on the same channel, so both message types
        // are processed concurrently.  A failure here means one of the consumer
        // bindings is broken or a shared resource (e.g., the channel itself) is
        // being consumed exclusively.
        //
        // "kafrsousa" is the designated position district for this test only — see
        // the district usage table at the top of this class.
        await ResetAsync();

        var deliveryId = Guid.NewGuid();
        const string district = "kafrsousa";

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId,
            district,
            "UnexpectedStop",
            "Driver parked for 20 min with no delivery action",
            DriverLat: 33.510,
            DriverLng: 36.280,
            DateTime.UtcNow));

        await PublishAsync(new DriverPositionIntegrationEvent(
            DriverId:       Guid.NewGuid(),
            DistrictId:     district,
            Lat:            33.510,
            Lng:            36.280,
            DeliveryStatus: "InTransit",
            Timestamp:      DateTime.UtcNow));

        var urgency  = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId}", TimeSpan.FromSeconds(30));
        var forecast = await WaitForCacheAsync<ForecastResultMessage>(
            $"forecast:{district}", TimeSpan.FromSeconds(30));

        urgency.Should().NotBeNull("anomaly pipeline must produce a result when active concurrently with the position pipeline");
        forecast.Should().NotBeNull("position pipeline must produce a result when active concurrently with the anomaly pipeline");
        urgency!.UrgencyScore.Should().Be(6,
            "UnexpectedStop (4) + kafrsousa district boost (2) = 6");
        urgency.DeliveryId.Should().Be(deliveryId);
        forecast!.Label.Should().BeOneOf("Critical", "Moderate", "Low demand");
        forecast.Color.Should().BeOneOf("#f87171", "#fbbf24", "#34d399");
    }

    [Test]
    [NotInParallel]
    public async Task RouteDeviation_InKafrsousa_Should_Produce_Score6()
    {
        // RouteDeviation (4) + kafrsousa district boost (2) = 6.
        // Pins the exact score for RouteDeviation (the other matrix entries —
        // StalePosition, EtaExceeded, UnexpectedStop, unknown — are already pinned;
        // RouteDeviation was previously only asserted as a range).
        await ResetAsync();

        var deliveryId = Guid.NewGuid();

        await PublishAsync(new DeliveryAnomalyIntegrationEvent(
            deliveryId,
            "kafrsousa",
            "RouteDeviation",
            "Driver 1.2 km off the planned route",
            DriverLat: 33.510,
            DriverLng: 36.280,
            DateTime.UtcNow));

        var urgency = await WaitForCacheAsync<UrgencyResultMessage>(
            $"urgency:{deliveryId}", TimeSpan.FromSeconds(30));

        urgency.Should().NotBeNull();
        urgency!.UrgencyScore.Should().Be(6, "RouteDeviation (4) + kafrsousa district boost (2) = 6");
        urgency.AiNote.Should().NotBeNullOrWhiteSpace();
    }
}
