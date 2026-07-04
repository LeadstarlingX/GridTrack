using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.UnitTests.CQRS.Handlers;
using GridTrack.Application.Dtos;
using GridTrack.Application.IntegrationEvents;
using GridTrack.Application.UseCases.Forecast;

namespace GridTrack.Application.UnitTests.UseCases.Forecast;

public class GetForecastHandlerTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ForecastDto MakeDto(int expectedDeliveries = 30, string districtId = "mezzeh") =>
        new(districtId, _now.AddHours(-1), expectedDeliveries, _now);

    private static ForecastResultMessage MakeMessage(int expectedDeliveries = 30, double staffingRatio = 0.1) =>
        new("mezzeh", expectedDeliveries, staffingRatio, "high", "#00ff00", _now);

    // ── Cache hit ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Handle_Returns_Cached_Response_When_Cache_Hit()
    {
        var cache = new FakeCacheService();
        var msg = MakeMessage();
        await cache.SetAsync("forecast:mezzeh", msg, TimeSpan.FromMinutes(1), CancellationToken.None);

        var handler = new GetForecastHandler();
        var result = await handler.Handle(
            new GetForecastQuery("mezzeh"),
            cache,
            new FakeForecastReadService(null),
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.DistrictId).IsEqualTo("mezzeh");
    }

    [Test]
    public async Task Handle_Does_Not_Call_ReadService_On_Cache_Hit()
    {
        var cache = new FakeCacheService();
        await cache.SetAsync("forecast:mezzeh", MakeMessage(), TimeSpan.FromMinutes(1), CancellationToken.None);

        var readService = new FakeForecastReadService(MakeDto());
        var handler = new GetForecastHandler();
        await handler.Handle(new GetForecastQuery("mezzeh"), cache, readService, CancellationToken.None);

        await Assert.That(readService.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Cache_Hit_Drivers_Needed_Uses_Cached_StaffingRatio()
    {
        var cache = new FakeCacheService();
        // 100 deliveries → ceiling(100/10) = 10 drivers
        await cache.SetAsync("forecast:mezzeh", MakeMessage(100, 0.1), TimeSpan.FromMinutes(1), CancellationToken.None);

        var handler = new GetForecastHandler();
        var result = await handler.Handle(new GetForecastQuery("mezzeh"), cache, new FakeForecastReadService(null), CancellationToken.None);

        await Assert.That(result!.DriverRecommendation).IsEqualTo(10);
    }

    // ── Cache miss → DB hit ───────────────────────────────────────────────────

    [Test]
    public async Task Handle_Returns_Null_When_ReadService_Returns_Null()
    {
        var handler = new GetForecastHandler();
        var result = await handler.Handle(
            new GetForecastQuery("mezzeh"),
            new FakeCacheService(),
            new FakeForecastReadService(null),
            CancellationToken.None);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Handle_Returns_Response_When_ReadService_Has_Data()
    {
        var handler = new GetForecastHandler();
        var result = await handler.Handle(
            new GetForecastQuery("mezzeh"),
            new FakeCacheService(),
            new FakeForecastReadService(MakeDto(40, "mezzeh")),
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.DistrictId).IsEqualTo("mezzeh");
        await Assert.That(result.ForecastedDemand).IsEqualTo(40);
    }

    [Test]
    public async Task Handle_Writes_To_Cache_After_DB_Hit()
    {
        var cache = new FakeCacheService();
        var handler = new GetForecastHandler();
        await handler.Handle(new GetForecastQuery("mezzeh"), cache, new FakeForecastReadService(MakeDto()), CancellationToken.None);

        await Assert.That(cache.SetCalls).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Handle_Drivers_Needed_Is_Ceiling_Of_Deliveries_Over_Ten()
    {
        // 25 deliveries → ceiling(25/10) = 3
        var handler = new GetForecastHandler();
        var result = await handler.Handle(
            new GetForecastQuery("mezzeh"),
            new FakeCacheService(),
            new FakeForecastReadService(MakeDto(25)),
            CancellationToken.None);

        await Assert.That(result!.DriverRecommendation).IsEqualTo(3);
    }

    [Test]
    public async Task Handle_Drivers_Needed_Is_At_Least_One()
    {
        // 0 deliveries → max(1, ceil(0/10)) = 1
        var handler = new GetForecastHandler();
        var result = await handler.Handle(
            new GetForecastQuery("mezzeh"),
            new FakeCacheService(),
            new FakeForecastReadService(MakeDto(0)),
            CancellationToken.None);

        await Assert.That(result!.DriverRecommendation).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_StaffingRatio_Is_One_When_Zero_Deliveries()
    {
        var handler = new GetForecastHandler();
        var result = await handler.Handle(
            new GetForecastQuery("mezzeh"),
            new FakeCacheService(),
            new FakeForecastReadService(MakeDto(0)),
            CancellationToken.None);

        await Assert.That(result!.StaffingRatio).IsEqualTo(1.0);
    }

    [Test]
    public async Task Handle_Horizon_Is_Next_Hour()
    {
        var handler = new GetForecastHandler();
        var result = await handler.Handle(
            new GetForecastQuery("mezzeh"),
            new FakeCacheService(),
            new FakeForecastReadService(MakeDto()),
            CancellationToken.None);

        await Assert.That(result!.Horizon).IsEqualTo("next-hour");
    }

    private sealed class FakeForecastReadService(ForecastDto? dto) : IForecastReadService
    {
        public int CallCount { get; private set; }

        public Task<ForecastDto?> GetForecastAsync(string districtId, DateTime forecastWindow, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(dto);
        }
    }
}
