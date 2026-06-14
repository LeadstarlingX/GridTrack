using FluentAssertions;
using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Ai;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class DistrictSummaryIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static Point Damascus => Geo.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static async Task SetCacheAsync<T>(string key, T value, TimeSpan expiry)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cache.SetAsync(key, value, expiry);
    }

    [Test]
    [NotInParallel(Order = 470)]
    public async Task GetDistrictSummary_Returns_Null_When_Ai_Unavailable_And_No_Stale_Cache()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<DistrictSummaryResponse?>(
            new GetDistrictSummaryQuery("mezzeh"));

        result.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 471)]
    public async Task GetDistrictSummary_Returns_Stale_Result_When_Fresh_Cache_Misses_And_Ai_Unavailable()
    {
        await ResetDatabaseAsync();

        var stale = new DistrictSummaryResponse(
            "mezzeh",
            "Increase driver coverage in the northwest sector.",
            DateTime.UtcNow.AddMinutes(-10),
            CachedAt: DateTime.UtcNow.AddMinutes(-10));

        await SetCacheAsync($"ai:district-summary:mezzeh:stale", stale, TimeSpan.FromHours(1));

        var result = await InvokeAsync<DistrictSummaryResponse?>(
            new GetDistrictSummaryQuery("mezzeh"));

        result.Should().NotBeNull();
        result!.DistrictId.Should().Be("mezzeh");
        result.Summary.Should().Be(stale.Summary);
    }

    [Test]
    [NotInParallel(Order = 472)]
    public async Task GetDistrictSummary_Returns_Cached_Result_Without_Calling_Ai()
    {
        await ResetDatabaseAsync();

        var fresh = new DistrictSummaryResponse(
            "malki",
            "Prioritise two overdue deliveries in the south.",
            DateTime.UtcNow,
            CachedAt: null);

        await SetCacheAsync($"ai:district-summary:malki", fresh, TimeSpan.FromMinutes(2));

        var result = await InvokeAsync<DistrictSummaryResponse?>(
            new GetDistrictSummaryQuery("malki"));

        result.Should().NotBeNull();
        result!.Summary.Should().Be(fresh.Summary);
        result.CachedAt.Should().BeNull(); // fresh entry retains null CachedAt
    }

    [Test]
    [NotInParallel(Order = 473)]
    public async Task GetDistrictContext_Correctly_Counts_Active_Deliveries_And_Drivers()
    {
        await ResetDatabaseAsync();

        var driver1 = MakeDriver("kafrsousa");
        var driver2 = MakeDriver("kafrsousa");
        var driver3 = MakeDriver("mezzeh");    // different district
        await SeedDriversAsync([driver1, driver2, driver3]);

        var activeDelivery    = MakeDelivery("kafrsousa", delivered: false);
        var deliveredDelivery = MakeDelivery("kafrsousa", delivered: true);
        await SeedDeliveriesAsync([activeDelivery, deliveredDelivery]);

        // Invoke the full query — AI is unavailable but context is assembled first
        await InvokeAsync<DistrictSummaryResponse?>(new GetDistrictSummaryQuery("kafrsousa"));

        // Verify the district context directly via the read service
        var ctx = await ResolveAsync<GridTrack.Application.CQRS.ReadServices.IDistrictReadService, GridTrack.Application.Dtos.DistrictContextDto>(
            svc => svc.GetDistrictContextAsync("kafrsousa", default));

        ctx.ActiveDeliveries.Should().Be(1);   // only the non-delivered one
        ctx.ActiveDrivers.Should().Be(2);       // only kafrsousa drivers
    }

    private static Driver MakeDriver(string district)
    {
        var r = Driver.Create(Guid.NewGuid(), Damascus, district, DateTime.UtcNow,
            "Test Driver", "TD", isActive: true);
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    private static Delivery MakeDelivery(string district, bool delivered)
    {
        var d = Delivery.Create(Guid.NewGuid(), Damascus, district, DateTime.UtcNow, null).Value;
        if (delivered)
        {
            d.AssignDriver(Guid.NewGuid());
            d.MarkPickedUp(Damascus, DateTime.UtcNow.AddMinutes(-30));
            d.MarkDelivered(DateTime.UtcNow.AddMinutes(-5));
        }
        d.ClearDomainEvents();
        return d;
    }
}
