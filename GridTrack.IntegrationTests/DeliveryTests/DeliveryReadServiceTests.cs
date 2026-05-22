using FluentAssertions;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DeliveryTests;

public class DeliveryReadServiceTests : BaseIntegrationTest
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

    // ── GetByIdAsync ──────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1)]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        await ResetDatabaseAsync();

        var service = GetReadService();

        var result = await service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 2)]
    public async Task GetByIdAsync_Should_Return_Delivery_When_Exists()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery();
        await SeedDeliveriesAsync([delivery]);

        var service = GetReadService();
        var result = await service.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.DeliveryId.Should().Be(delivery.DeliveryId);
        result.DistrictId.Should().Be(delivery.DistrictId);
        result.Status.Should().Be(DeliveryStatus.Created);
        result.AssignedDriverId.Should().BeNull();
        result.AnomalyFlag.Should().BeFalse();
    }

    [Test]
    [NotInParallel(Order = 3)]
    public async Task GetByIdAsync_Should_Return_Correct_Location()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery();
        await SeedDeliveriesAsync([delivery]);

        var service = GetReadService();
        var result = await service.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CurrentLocation.Should().NotBeNull();
        result.CurrentLocation.Coordinate.X.Should().BeApproximately(36.2765, precision: 0.0001);
        result.CurrentLocation.Coordinate.Y.Should().BeApproximately(33.5138, precision: 0.0001);
    }

    [Test]
    [NotInParallel(Order = 4)]
    public async Task GetByIdAsync_Should_Return_Correct_Status_After_Driver_Assigned()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.ClearDomainEvents();
        await SeedDeliveriesAsync([delivery]);

        var service = GetReadService();
        var result = await service.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(DeliveryStatus.Assigned);
        result.AssignedDriverId.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 5)]
    public async Task GetByIdAsync_Should_Reflect_Anomaly_Flag()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());

        var point = GeoFactory.CreatePoint(new Coordinate(36.28, 33.52));
        delivery.MarkPickedUp(point, DateTime.UtcNow);
        delivery.UpdateLocation(point, DateTime.UtcNow);
        delivery.FlagAnomaly(AnomalyType.EtaExceeded, "ETA exceeded by 30 minutes");
        delivery.ClearDomainEvents();

        await SeedDeliveriesAsync([delivery]);

        var service = GetReadService();
        var result = await service.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.AnomalyFlag.Should().BeTrue();
        result.AnomalyReason.Should().Be("ETA exceeded by 30 minutes");
        result.Status.Should().Be(DeliveryStatus.Anomalous);
    }

    // ── GetByDistrictAsync ────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 6)]
    public async Task GetByDistrictAsync_Should_Return_Empty_When_No_Deliveries_In_District()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery(districtId: "h3-district-A");
        await SeedDeliveriesAsync([delivery]);

        var service = GetReadService();
        var result = (await service.GetByDistrictAsync("h3-district-B", CancellationToken.None))
            .ToList();

        result.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 7)]
    public async Task GetByDistrictAsync_Should_Return_Only_Deliveries_In_District()
    {
        await ResetDatabaseAsync();

        var d1 = CreateDelivery(districtId: "h3-target");
        var d2 = CreateDelivery(districtId: "h3-target");
        var d3 = CreateDelivery(districtId: "h3-other");
        await SeedDeliveriesAsync([d1, d2, d3]);

        var service = GetReadService();
        var result = (await service.GetByDistrictAsync("h3-target", CancellationToken.None))
            .ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(d => d.DistrictId == "h3-target");
    }

    [Test]
    [NotInParallel(Order = 8)]
    public async Task GetByDistrictAsync_Should_Return_Results_Ordered_By_CreatedAt_Descending()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        var older  = CreateDelivery(districtId: "h3-order", createdAt: now.AddMinutes(-10));
        var newer  = CreateDelivery(districtId: "h3-order", createdAt: now.AddMinutes(-1));
        var newest = CreateDelivery(districtId: "h3-order", createdAt: now);

        await SeedDeliveriesAsync([older, newer, newest]);

        var service = GetReadService();
        var result = (await service.GetByDistrictAsync("h3-order", CancellationToken.None))
            .ToList();

        result.Should().HaveCount(3);
        result[0].DeliveryId.Should().Be(newest.DeliveryId);
        result[1].DeliveryId.Should().Be(newer.DeliveryId);
        result[2].DeliveryId.Should().Be(older.DeliveryId);
    }

    [Test]
    [NotInParallel(Order = 9)]
    public async Task GetByDistrictAsync_Should_Return_Empty_When_District_Has_No_Data()
    {
        await ResetDatabaseAsync();

        var service = GetReadService();
        var result = (await service.GetByDistrictAsync("h3-nonexistent", CancellationToken.None))
            .ToList();

        result.Should().BeEmpty();
    }
}