using FluentAssertions;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Domain.Drivers;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DriverTests;

public class DriverReadServiceTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);

    // ── Fixed coordinates ─────────────────────────────────────────────────
    // Damascus center
    private static Point Damascus  => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));
    // ~1 km north of Damascus center
    private static Point NearPoint => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5230));
    // Aleppo (~350 km away)
    private static Point Aleppo    => GeoFactory.CreatePoint(new Coordinate(37.1611, 36.2021));

    // ── Helpers ───────────────────────────────────────────────────────────

    private static IDriverReadService GetReadService()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDriverReadService>();
    }

    private static Driver CreateDriver(
        Point? location     = null,
        string districtId   = "h3-district-01",
        bool isActive       = true,
        DateTime? lastSeen  = null)
    {
        var result = Driver.Create(
            Guid.NewGuid(),
            location ?? Damascus,
            districtId,
            lastSeen ?? DateTime.UtcNow,
            isActive);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    // ── GetByDistrictAsync ────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 10)]
    public async Task GetByDistrictAsync_Should_Return_Empty_When_No_Drivers()
    {
        await ResetDatabaseAsync();

        var service = GetReadService();
        var result = (await service.GetByDistrictAsync("h3-district-01", CancellationToken.None))
            .ToList();

        result.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 11)]
    public async Task GetByDistrictAsync_Should_Return_Drivers_In_District()
    {
        await ResetDatabaseAsync();

        var d1 = CreateDriver(districtId: "h3-target");
        var d2 = CreateDriver(districtId: "h3-target");
        var d3 = CreateDriver(districtId: "h3-other");
        await SeedDriversAsync([d1, d2, d3]);

        var service = GetReadService();
        var result = (await service.GetByDistrictAsync("h3-target", CancellationToken.None))
            .ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(d => d.DistrictId == "h3-target");
    }

    [Test]
    [NotInParallel(Order = 12)]
    public async Task GetByDistrictAsync_Should_Return_Both_Active_And_Inactive_Drivers()
    {
        await ResetDatabaseAsync();

        var active   = CreateDriver(districtId: "h3-mix", isActive: true);
        var inactive = CreateDriver(districtId: "h3-mix", isActive: false);
        await SeedDriversAsync([active, inactive]);

        var service = GetReadService();
        var result = (await service.GetByDistrictAsync("h3-mix", CancellationToken.None))
            .ToList();

        // GetByDistrictAsync has no isActive filter — returns all in district
        result.Should().HaveCount(2);
        result.Should().Contain(d => d.IsActive);
        result.Should().Contain(d => !d.IsActive);
    }

    [Test]
    [NotInParallel(Order = 13)]
    public async Task GetByDistrictAsync_Should_Return_Results_Ordered_By_LastSeen_Descending()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        var stale  = CreateDriver(districtId: "h3-order", lastSeen: now.AddMinutes(-30));
        var recent = CreateDriver(districtId: "h3-order", lastSeen: now.AddMinutes(-5));
        var latest = CreateDriver(districtId: "h3-order", lastSeen: now);

        await SeedDriversAsync([stale, recent, latest]);

        var service = GetReadService();
        var result = (await service.GetByDistrictAsync("h3-order", CancellationToken.None))
            .ToList();

        result.Should().HaveCount(3);
        result[0].DriverId.Should().Be(latest.DriverId);
        result[1].DriverId.Should().Be(recent.DriverId);
        result[2].DriverId.Should().Be(stale.DriverId);
    }

    [Test]
    [NotInParallel(Order = 14)]
    public async Task GetByDistrictAsync_Should_Return_Correct_Location()
    {
        await ResetDatabaseAsync();

        var driver = CreateDriver(location: Damascus, districtId: "h3-loc");
        await SeedDriversAsync([driver]);

        var service = GetReadService();
        var result = (await service.GetByDistrictAsync("h3-loc", CancellationToken.None))
            .ToList();

        result.Should().HaveCount(1);
        result[0].Location.Coordinate.X.Should().BeApproximately(36.2765, precision: 0.0001);
        result[0].Location.Coordinate.Y.Should().BeApproximately(33.5138, precision: 0.0001);
    }

    // ── GetNearestAsync ───────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 15)]
    public async Task GetNearestAsync_Should_Return_Empty_When_No_Active_Drivers()
    {
        await ResetDatabaseAsync();

        var inactive = CreateDriver(location: Damascus, isActive: false);
        await SeedDriversAsync([inactive]);

        var service = GetReadService();
        var result = (await service.GetNearestAsync(Damascus, count: 5, CancellationToken.None))
            .ToList();

        result.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 16)]
    public async Task GetNearestAsync_Should_Return_Only_Active_Drivers()
    {
        await ResetDatabaseAsync();

        var active   = CreateDriver(location: Damascus,  isActive: true);
        var inactive = CreateDriver(location: NearPoint, isActive: false);
        await SeedDriversAsync([active, inactive]);

        var service = GetReadService();
        var result = (await service.GetNearestAsync(Damascus, count: 5, CancellationToken.None))
            .ToList();

        result.Should().HaveCount(1);
        result[0].DriverId.Should().Be(active.DriverId);
        result[0].IsActive.Should().BeTrue();
    }

    [Test]
    [NotInParallel(Order = 17)]
    public async Task GetNearestAsync_Should_Respect_Count_Limit()
    {
        await ResetDatabaseAsync();

        var drivers = Enumerable.Range(0, 5)
            .Select(_ => CreateDriver(location: Damascus, isActive: true))
            .ToList();

        await SeedDriversAsync(drivers);

        var service = GetReadService();
        var result = (await service.GetNearestAsync(Damascus, count: 3, CancellationToken.None))
            .ToList();

        result.Should().HaveCount(3);
    }

    [Test]
    [NotInParallel(Order = 18)]
    public async Task GetNearestAsync_Should_Return_Closest_Driver_First()
    {
        await ResetDatabaseAsync();

        // NearPoint is ~1 km from Damascus; Aleppo is ~350 km away
        var nearDriver = CreateDriver(location: NearPoint, isActive: true);
        var farDriver  = CreateDriver(location: Aleppo,    isActive: true);
        await SeedDriversAsync([nearDriver, farDriver]);

        var service = GetReadService();
        var result = (await service.GetNearestAsync(Damascus, count: 2, CancellationToken.None))
            .ToList();

        result.Should().HaveCount(2);
        result[0].DriverId.Should().Be(nearDriver.DriverId);
        result[1].DriverId.Should().Be(farDriver.DriverId);
    }

    [Test]
    [NotInParallel(Order = 19)]
    public async Task GetNearestAsync_Should_Return_Empty_When_No_Drivers_Exist()
    {
        await ResetDatabaseAsync();

        var service = GetReadService();
        var result = (await service.GetNearestAsync(Damascus, count: 5, CancellationToken.None))
            .ToList();

        result.Should().BeEmpty();
    }
}