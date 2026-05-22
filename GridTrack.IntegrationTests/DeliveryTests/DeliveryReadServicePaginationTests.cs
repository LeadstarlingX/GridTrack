using FluentAssertions;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DeliveryTests;

public class DeliveryReadServicePaginationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);

    // ── Damascus coordinates (lng=36.2765, lat=33.5138) ──────────────────
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    // ── Helpers ───────────────────────────────────────────────────────────

    private static IDeliveryReadService GetReadService()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDeliveryReadService>();
    }

    private static Delivery CreateDelivery(
        string districtId = "h3-district-01",
        DateTime? createdAt = null)
    {
        var result = Delivery.Create(
            Guid.NewGuid(),
            Damascus,
            districtId,
            createdAt ?? DateTime.UtcNow,
            expectedEta: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    // ── Pagination scenarios for GetByDistrictAsync ───────────────────────

    [Test]
    [NotInParallel(Order = 20)]
    public async Task GetByDistrictAsync_WithLargeDataset_Should_Return_All_Deliveries_In_District()
    {
        await ResetDatabaseAsync();

        // Create 100 deliveries in the same district
        var deliveries = Enumerable.Range(0, 100)
            .Select(i => CreateDelivery(districtId: "h3-pagination", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        await SeedDeliveriesAsync(deliveries);

        var service = GetReadService();
        var results = (await service.GetByDistrictAsync("h3-pagination", CancellationToken.None))
            .ToList();

        results.Should().HaveCount(100);
        results.Should().OnlyContain(d => d.DistrictId == "h3-pagination");
    }

    [Test]
    [NotInParallel(Order = 21)]
    public async Task GetByDistrictAsync_Should_Maintain_Consistent_Ordering_Across_Multiple_Calls()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        var deliveries = Enumerable.Range(0, 50)
            .Select(i => CreateDelivery(districtId: "h3-order-test", createdAt: now.AddMinutes(-i)))
            .ToList();

        await SeedDeliveriesAsync(deliveries);

        var service = GetReadService();

        var firstCall = (await service.GetByDistrictAsync("h3-order-test", CancellationToken.None))
            .Select(d => d.DeliveryId)
            .ToList();

        var secondCall = (await service.GetByDistrictAsync("h3-order-test", CancellationToken.None))
            .Select(d => d.DeliveryId)
            .ToList();

        firstCall.Should().BeEquivalentTo(secondCall, options => options.WithStrictOrdering());
    }

    [Test]
    [NotInParallel(Order = 22)]
    public async Task GetByDistrictAsync_WithMultipleDistricts_Should_Isolate_Results()
    {
        await ResetDatabaseAsync();

        var districtADeliveries = Enumerable.Range(0, 40)
            .Select(i => CreateDelivery(districtId: "h3-district-a", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        var districtBDeliveries = Enumerable.Range(0, 60)
            .Select(i => CreateDelivery(districtId: "h3-district-b", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        var districtCDeliveries = Enumerable.Range(0, 25)
            .Select(i => CreateDelivery(districtId: "h3-district-c", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        await SeedDeliveriesAsync(districtADeliveries.Concat(districtBDeliveries).Concat(districtCDeliveries));

        var service = GetReadService();

        var resultsA = (await service.GetByDistrictAsync("h3-district-a", CancellationToken.None)).ToList();
        var resultsB = (await service.GetByDistrictAsync("h3-district-b", CancellationToken.None)).ToList();
        var resultsC = (await service.GetByDistrictAsync("h3-district-c", CancellationToken.None)).ToList();

        resultsA.Should().HaveCount(40);
        resultsB.Should().HaveCount(60);
        resultsC.Should().HaveCount(25);

        // Ensure no cross-contamination
        resultsA.Should().OnlyContain(d => d.DistrictId == "h3-district-a");
        resultsB.Should().OnlyContain(d => d.DistrictId == "h3-district-b");
        resultsC.Should().OnlyContain(d => d.DistrictId == "h3-district-c");
    }

    [Test]
    [NotInParallel(Order = 23)]
    public async Task GetByDistrictAsync_WithDifferentStatuses_Should_Return_All_Statuses()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        var createdDeliveries = Enumerable.Range(0, 20)
            .Select(i => CreateDelivery(districtId: "h3-status-mix", createdAt: now.AddMinutes(-i)))
            .ToList();

        // Assign some deliveries to change their status
        var assignedDeliveries = Enumerable.Range(0, 15)
            .Select(i =>
            {
                var delivery = CreateDelivery(districtId: "h3-status-mix", createdAt: now.AddMinutes(-i));
                delivery.AssignDriver(Guid.NewGuid());
                return delivery;
            })
            .ToList();

        await SeedDeliveriesAsync(createdDeliveries.Concat(assignedDeliveries));

        var service = GetReadService();
        var results = (await service.GetByDistrictAsync("h3-status-mix", CancellationToken.None))
            .ToList();

        results.Should().HaveCount(35);
        results.Should().Contain(d => d.Status == DeliveryStatus.Created);
        results.Should().Contain(d => d.Status == DeliveryStatus.Assigned);
    }

    [Test]
    [NotInParallel(Order = 24)]
    public async Task GetByDistrictAsync_WithAnomalies_Should_Return_All_Deliveries_Regardless_Of_Anomaly_Flag()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        var normalDeliveries = Enumerable.Range(0, 25)
            .Select(i => CreateDelivery(districtId: "h3-anomaly-mix", createdAt: now.AddMinutes(-i)))
            .ToList();

        var anomalousDeliveries = Enumerable.Range(0, 10)
            .Select(i =>
            {
                var delivery = CreateDelivery(districtId: "h3-anomaly-mix", createdAt: now.AddMinutes(-i));
                delivery.AssignDriver(Guid.NewGuid());
                var point = GeoFactory.CreatePoint(new Coordinate(36.28, 33.52));
                delivery.MarkPickedUp(point, DateTime.UtcNow);
                delivery.FlagAnomaly(Domain.ValueObjects.AnomalyType.EtaExceeded, "Test anomaly");
                return delivery;
            })
            .ToList();

        await SeedDeliveriesAsync(normalDeliveries.Concat(anomalousDeliveries));

        var service = GetReadService();
        var results = (await service.GetByDistrictAsync("h3-anomaly-mix", CancellationToken.None))
            .ToList();

        results.Should().HaveCount(35);
        results.Should().Contain(d => d.AnomalyFlag);
        results.Should().Contain(d => !d.AnomalyFlag);
    }

    [Test]
    [NotInParallel(Order = 25)]
    public async Task GetByDistrictAsync_EmptyDistrict_Should_Return_Empty_Result()
    {
        await ResetDatabaseAsync();

        // Seed deliveries in other districts only
        var otherDeliveries = Enumerable.Range(0, 10)
            .Select(i => CreateDelivery(districtId: "h3-other", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        await SeedDeliveriesAsync(otherDeliveries);

        var service = GetReadService();
        var results = (await service.GetByDistrictAsync("h3-empty", CancellationToken.None))
            .ToList();

        results.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 26)]
    public async Task GetByDistrictAsync_WithVeryLargeDataset_Should_Perform_Reasonably()
    {
        await ResetDatabaseAsync();

        // Create 500 deliveries to test performance with larger datasets
        var deliveries = Enumerable.Range(0, 500)
            .Select(i => CreateDelivery(districtId: "h3-performance", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        await SeedDeliveriesAsync(deliveries);

        var service = GetReadService();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = (await service.GetByDistrictAsync("h3-performance", CancellationToken.None))
            .ToList();
        stopwatch.Stop();

        results.Should().HaveCount(500);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
    }
}