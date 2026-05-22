using FluentAssertions;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Drivers;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DriverTests;

public class DriverRepositoryTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
 
    private static Point Damascus  => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));
    private static Point NearPoint => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5230));
    private static Point Aleppo    => GeoFactory.CreatePoint(new Coordinate(37.1611, 36.2021));
 
    // ── Helpers ───────────────────────────────────────────────────────────
 
    private static Driver CreateDriver(
        Point? location   = null,
        string districtId = "h3-district-01",
        bool isActive     = true)
    {
        var result = Driver.Create(
            Guid.NewGuid(),
            location ?? Damascus,
            districtId,
            DateTime.UtcNow,
            isActive);
 
        result.IsSuccess.Should().BeTrue();
        result.Value.ClearDomainEvents();
        return result.Value;
    }
 
    private static IDriverRepository GetRepository()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDriverRepository>();
    }
 
    // ── AddAsync + GetByIdAsync ───────────────────────────────────────────
 
    [Test]
    [NotInParallel(Order = 30)]
    public async Task AddAsync_Then_GetByIdAsync_Should_Persist_And_Retrieve_Driver()
    {
        await ResetDatabaseAsync();
 
        var driver = CreateDriver();
        await SeedDriversAsync([driver]);
 
        var repository = GetRepository();
        var retrieved = await repository.GetByIdAsync(driver.DriverId, CancellationToken.None);
 
        retrieved.Should().NotBeNull();
        retrieved!.DriverId.Should().Be(driver.DriverId);
        retrieved.DistrictId.Should().Be(driver.DistrictId);
        retrieved.IsActive.Should().BeTrue();
    }
 
    [Test]
    [NotInParallel(Order = 31)]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        await ResetDatabaseAsync();
 
        var repository = GetRepository();
        var result = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);
 
        result.Should().BeNull();
    }
 
    // ── UpdateAsync ───────────────────────────────────────────────────────
 
    [Test]
    [NotInParallel(Order = 32)]
    public async Task UpdateAsync_Should_Persist_Availability_Change()
    {
        await ResetDatabaseAsync();
 
        var driver = CreateDriver(isActive: true);
        await SeedDriversAsync([driver]);
 
        driver.SetAvailability(false);
        driver.ClearDomainEvents();
 
        await SeedAsync(ctx =>
        {
            ctx.Set<Driver>().Update(driver);
            return Task.CompletedTask;
        });
 
        var repository = GetRepository();
        var retrieved = await repository.GetByIdAsync(driver.DriverId, CancellationToken.None);
 
        retrieved.Should().NotBeNull();
        retrieved!.IsActive.Should().BeFalse();
    }
 
    [Test]
    [NotInParallel(Order = 33)]
    public async Task UpdateAsync_Should_Persist_Position_Update()
    {
        await ResetDatabaseAsync();
 
        var driver = CreateDriver(location: Damascus);
        await SeedDriversAsync([driver]);
 
        var updatedAt = DateTime.UtcNow;
        driver.UpdatePosition(NearPoint, updatedAt);
        driver.ClearDomainEvents();
 
        await SeedAsync(ctx =>
        {
            ctx.Set<Driver>().Update(driver);
            return Task.CompletedTask;
        });
 
        var repository = GetRepository();
        var retrieved = await repository.GetByIdAsync(driver.DriverId, CancellationToken.None);
 
        retrieved.Should().NotBeNull();
        retrieved!.Location.X.Should().BeApproximately(NearPoint.X, precision: 0.0001);
        retrieved.Location.Y.Should().BeApproximately(NearPoint.Y, precision: 0.0001);
        retrieved.LastSeen.Should().BeCloseTo(updatedAt, precision: TimeSpan.FromSeconds(1));
    }
 
    // ── GetActiveByDistrictAsync ──────────────────────────────────────────
 
    [Test]
    [NotInParallel(Order = 34)]
    public async Task GetActiveByDistrictAsync_Should_Return_Only_Active_Drivers()
    {
        await ResetDatabaseAsync();
 
        var active   = CreateDriver(districtId: "h3-filter", isActive: true);
        var inactive = CreateDriver(districtId: "h3-filter", isActive: false);
        await SeedDriversAsync([active, inactive]);
 
        var repository = GetRepository();
        var results = (await repository.GetActiveByDistrictAsync("h3-filter", CancellationToken.None))
            .ToList();
 
        results.Should().HaveCount(1);
        results[0].DriverId.Should().Be(active.DriverId);
    }
 
    [Test]
    [NotInParallel(Order = 35)]
    public async Task GetActiveByDistrictAsync_Should_Return_Empty_For_Unknown_District()
    {
        await ResetDatabaseAsync();
 
        var driver = CreateDriver(districtId: "h3-known");
        await SeedDriversAsync([driver]);
 
        var repository = GetRepository();
        var results = (await repository.GetActiveByDistrictAsync("h3-unknown", CancellationToken.None))
            .ToList();
 
        results.Should().BeEmpty();
    }
 
    // ── GetNearestAsync ───────────────────────────────────────────────────
 
    [Test]
    [NotInParallel(Order = 36)]
    public async Task GetNearestAsync_Should_Return_Closest_Driver_First()
    {
        await ResetDatabaseAsync();
 
        var nearDriver = CreateDriver(location: NearPoint, isActive: true);
        var farDriver  = CreateDriver(location: Aleppo,    isActive: true);
        await SeedDriversAsync([nearDriver, farDriver]);
 
        var repository = GetRepository();
        var results = (await repository.GetNearestAsync(Damascus, count: 2, CancellationToken.None))
            .ToList();
 
        results.Should().HaveCount(2);
        results[0].DriverId.Should().Be(nearDriver.DriverId);
        results[1].DriverId.Should().Be(farDriver.DriverId);
    }
 
    [Test]
    [NotInParallel(Order = 37)]
    public async Task GetNearestAsync_Should_Respect_Count_Limit()
    {
        await ResetDatabaseAsync();
 
        var drivers = Enumerable.Range(0, 6)
            .Select(_ => CreateDriver(isActive: true))
            .ToList();
 
        await SeedDriversAsync(drivers);
 
        var repository = GetRepository();
        var results = (await repository.GetNearestAsync(Damascus, count: 4, CancellationToken.None))
            .ToList();
 
        results.Should().HaveCount(4);
    }
 
    [Test]
    [NotInParallel(Order = 38)]
    public async Task GetNearestAsync_Should_Return_Only_Active_Drivers()
    {
        await ResetDatabaseAsync();
 
        var active   = CreateDriver(location: Damascus,  isActive: true);
        var inactive = CreateDriver(location: NearPoint, isActive: false);
        await SeedDriversAsync([active, inactive]);
 
        var repository = GetRepository();
        var results = (await repository.GetNearestAsync(Damascus, count: 10, CancellationToken.None))
            .ToList();
 
        results.Should().HaveCount(1);
        results[0].DriverId.Should().Be(active.DriverId);
    }
 
    [Test]
    [NotInParallel(Order = 39)]
    public async Task GetNearestAsync_Should_Return_Empty_When_No_Active_Drivers()
    {
        await ResetDatabaseAsync();
 
        var inactive = CreateDriver(isActive: false);
        await SeedDriversAsync([inactive]);
 
        var repository = GetRepository();
        var results = (await repository.GetNearestAsync(Damascus, count: 5, CancellationToken.None))
            .ToList();
 
        results.Should().BeEmpty();
    }
}