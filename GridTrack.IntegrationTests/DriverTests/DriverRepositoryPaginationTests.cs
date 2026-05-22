using FluentAssertions;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Drivers;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DriverTests;

public class DriverRepositoryPaginationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);

    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Driver CreateDriver(
        string districtId = "h3-district-01",
        bool isActive = true)
    {
        var location = GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138 + (districtId.GetHashCode() % 100) * 0.001));
        var result = Driver.Create(
            Guid.NewGuid(),
            location,
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

    // ── Pagination: Skip/Take scenarios ───────────────────────────────────

    [Test]
    [NotInParallel(Order = 40)]
    public async Task GetActiveByDistrictAsync_WithLargeDataset_Should_Return_All_Matching_Drivers()
    {
        await ResetDatabaseAsync();

        // Create 50 drivers in the same district
        var drivers = Enumerable.Range(0, 50)
            .Select(_ => CreateDriver(districtId: "h3-pagination", isActive: true))
            .ToList();

        await SeedDriversAsync(drivers);

        var repository = GetRepository();
        var results = (await repository.GetActiveByDistrictAsync("h3-pagination", CancellationToken.None))
            .ToList();

        results.Should().HaveCount(50);
        results.Should().OnlyContain(d => d.IsActive);
        results.Should().OnlyContain(d => d.DistrictId == "h3-pagination");
    }

    [Test]
    [NotInParallel(Order = 41)]
    public async Task GetNearestAsync_WithLargeDataset_Should_Respect_Count_Parameter()
    {
        await ResetDatabaseAsync();

        // Create 100 active drivers at varying distances
        var drivers = Enumerable.Range(0, 100)
            .Select(i =>
            {
                var offset = i * 0.0001; // Small offset to vary distance
                var location = GeoFactory.CreatePoint(new Coordinate(36.2765 + offset, 33.5138 + offset));
                return CreateDriver(isActive: true);
            })
            .ToList();

        await SeedDriversAsync(drivers);

        var repository = GetRepository();

        // Request only 10 nearest
        var tenResults = (await repository.GetNearestAsync(Damascus, count: 10, CancellationToken.None))
            .ToList();
        tenResults.Should().HaveCount(10);

        // Request 50 nearest
        var fiftyResults = (await repository.GetNearestAsync(Damascus, count: 50, CancellationToken.None))
            .ToList();
        fiftyResults.Should().HaveCount(50);

        // Request all
        var allResults = (await repository.GetNearestAsync(Damascus, count: 200, CancellationToken.None))
            .ToList();
        allResults.Should().HaveCount(100); // Only 100 exist
    }

    [Test]
    [NotInParallel(Order = 42)]
    public async Task GetNearestAsync_Should_Return_Consistent_Ordering_Across_Multiple_Calls()
    {
        await ResetDatabaseAsync();

        var drivers = Enumerable.Range(0, 20)
            .Select(i =>
            {
                var offset = i * 0.001;
                var location = GeoFactory.CreatePoint(new Coordinate(36.2765 + offset, 33.5138 + offset));
                return CreateDriver(isActive: true);
            })
            .ToList();

        await SeedDriversAsync(drivers);

        var repository = GetRepository();

        var firstCall = (await repository.GetNearestAsync(Damascus, count: 20, CancellationToken.None))
            .Select(d => d.DriverId)
            .ToList();

        var secondCall = (await repository.GetNearestAsync(Damascus, count: 20, CancellationToken.None))
            .Select(d => d.DriverId)
            .ToList();

        firstCall.Should().BeEquivalentTo(secondCall, options => options.WithStrictOrdering());
    }

    [Test]
    [NotInParallel(Order = 43)]
    public async Task GetActiveByDistrictAsync_Should_Filter_Correctly_With_Mixed_Active_Status()
    {
        await ResetDatabaseAsync();

        var activeDrivers = Enumerable.Range(0, 30)
            .Select(_ => CreateDriver(districtId: "h3-mix", isActive: true))
            .ToList();

        var inactiveDrivers = Enumerable.Range(0, 20)
            .Select(_ => CreateDriver(districtId: "h3-mix", isActive: false))
            .ToList();

        await SeedDriversAsync(activeDrivers.Concat(inactiveDrivers));

        var repository = GetRepository();
        var results = (await repository.GetActiveByDistrictAsync("h3-mix", CancellationToken.None))
            .ToList();

        results.Should().HaveCount(30);
        results.Should().OnlyContain(d => d.IsActive);
    }

    [Test]
    [NotInParallel(Order = 44)]
    public async Task GetNearestAsync_WithZeroCount_Should_Return_Empty()
    {
        await ResetDatabaseAsync();

        var drivers = Enumerable.Range(0, 10)
            .Select(_ => CreateDriver(isActive: true))
            .ToList();

        await SeedDriversAsync(drivers);

        var repository = GetRepository();
        var results = (await repository.GetNearestAsync(Damascus, count: 0, CancellationToken.None))
            .ToList();

        results.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 45)]
    public async Task GetActiveByDistrictAsync_WithMultipleDistricts_Should_Isolate_Results()
    {
        await ResetDatabaseAsync();

        var districtADrivers = Enumerable.Range(0, 25)
            .Select(_ => CreateDriver(districtId: "h3-district-a", isActive: true))
            .ToList();

        var districtBDrivers = Enumerable.Range(0, 35)
            .Select(_ => CreateDriver(districtId: "h3-district-b", isActive: true))
            .ToList();

        var districtCDrivers = Enumerable.Range(0, 15)
            .Select(_ => CreateDriver(districtId: "h3-district-c", isActive: true))
            .ToList();

        await SeedDriversAsync(districtADrivers.Concat(districtBDrivers).Concat(districtCDrivers));

        var repository = GetRepository();

        var resultsA = (await repository.GetActiveByDistrictAsync("h3-district-a", CancellationToken.None)).ToList();
        var resultsB = (await repository.GetActiveByDistrictAsync("h3-district-b", CancellationToken.None)).ToList();
        var resultsC = (await repository.GetActiveByDistrictAsync("h3-district-c", CancellationToken.None)).ToList();

        resultsA.Should().HaveCount(25);
        resultsB.Should().HaveCount(35);
        resultsC.Should().HaveCount(15);

        // Ensure no cross-contamination
        resultsA.Should().OnlyContain(d => d.DistrictId == "h3-district-a");
        resultsB.Should().OnlyContain(d => d.DistrictId == "h3-district-b");
        resultsC.Should().OnlyContain(d => d.DistrictId == "h3-district-c");
    }
}