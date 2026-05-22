using FluentAssertions;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Domain.Drivers;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DriverTests;

public class DriverRepositoryPaginationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);

    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

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
            "Ahmad Hassan",
            "Ahmad",
            isActive);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    private static IDriverReadService GetReadService()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDriverReadService>();
    }

    [Test]
    [NotInParallel(Order = 40)]
    public async Task GetByDistrictAsync_WithLargeDataset_Should_Return_All_Matching_Drivers()
    {
        await ResetDatabaseAsync();

        var drivers = Enumerable.Range(0, 50)
            .Select(_ => CreateDriver(districtId: "h3-pagination", isActive: true))
            .ToList();

        await SeedDriversAsync(drivers);

        var readService = GetReadService();
        var results = (await readService.GetByDistrictAsync("h3-pagination", CancellationToken.None))
            .ToList();

        results.Should().HaveCount(50);
        results.Should().OnlyContain(d => d.DistrictId == "h3-pagination");
    }

    [Test]
    [NotInParallel(Order = 41)]
    public async Task GetNearestAsync_WithLargeDataset_Should_Respect_Count_Parameter()
    {
        await ResetDatabaseAsync();

        var drivers = Enumerable.Range(0, 100)
            .Select(i =>
            {
                var offset = i * 0.0001;
                var location = GeoFactory.CreatePoint(new Coordinate(36.2765 + offset, 33.5138 + offset));
                return CreateDriver(isActive: true);
            })
            .ToList();

        await SeedDriversAsync(drivers);

        var readService = GetReadService();

        var tenResults = (await readService.GetNearestAsync(Damascus, count: 10, CancellationToken.None))
            .ToList();
        tenResults.Should().HaveCount(10);

        var fiftyResults = (await readService.GetNearestAsync(Damascus, count: 50, CancellationToken.None))
            .ToList();
        fiftyResults.Should().HaveCount(50);

        var allResults = (await readService.GetNearestAsync(Damascus, count: 200, CancellationToken.None))
            .ToList();
        allResults.Should().HaveCount(100);
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

        var readService = GetReadService();

        var firstCall = (await readService.GetNearestAsync(Damascus, count: 20, CancellationToken.None))
            .Select(d => d.DriverId)
            .ToList();

        var secondCall = (await readService.GetNearestAsync(Damascus, count: 20, CancellationToken.None))
            .Select(d => d.DriverId)
            .ToList();

        firstCall.Should().BeEquivalentTo(secondCall, options => options.WithStrictOrdering());
    }

    [Test]
    [NotInParallel(Order = 43)]
    public async Task GetByDistrictAsync_WithMultipleDistricts_Should_Isolate_Results()
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

        var readService = GetReadService();

        var resultsA = (await readService.GetByDistrictAsync("h3-district-a", CancellationToken.None)).ToList();
        var resultsB = (await readService.GetByDistrictAsync("h3-district-b", CancellationToken.None)).ToList();
        var resultsC = (await readService.GetByDistrictAsync("h3-district-c", CancellationToken.None)).ToList();

        resultsA.Should().HaveCount(25);
        resultsB.Should().HaveCount(35);
        resultsC.Should().HaveCount(15);

        resultsA.Should().OnlyContain(d => d.DistrictId == "h3-district-a");
        resultsB.Should().OnlyContain(d => d.DistrictId == "h3-district-b");
        resultsC.Should().OnlyContain(d => d.DistrictId == "h3-district-c");
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

        var readService = GetReadService();
        var results = (await readService.GetNearestAsync(Damascus, count: 0, CancellationToken.None))
            .ToList();

        results.Should().BeEmpty();
    }
}
