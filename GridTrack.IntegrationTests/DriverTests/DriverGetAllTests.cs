using FluentAssertions;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Domain.ValueObjects;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DriverTests;

public class DriverGetAllTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static Driver MakeDriver(
        string name = "Ahmad Hassan",
        string shortName = "Ahmad",
        string district = "mezzeh",
        bool isActive = true)
    {
        var d = Driver.Create(Guid.NewGuid(), Damascus, district, DateTime.UtcNow, name, shortName, isActive).Value;
        d.ClearDomainEvents();
        return d;
    }

    private static Delivery MakeDelivery(Guid driverId, string district, DeliveryStatus targetStatus,
        bool anomaly = false)
    {
        var d = Delivery.Create(Guid.NewGuid(), Damascus, district, DateTime.UtcNow, null).Value;
        d.AssignDriver(driverId);
        d.MarkPickedUp(Damascus, DateTime.UtcNow);
        d.UpdateLocation(Damascus, DateTime.UtcNow); // → InTransit

        if (anomaly)
            d.FlagAnomaly(AnomalyType.EtaExceeded, "ETA exceeded by 20 min");
        else if (targetStatus == DeliveryStatus.Delivered)
            d.MarkDelivered(DateTime.UtcNow);

        d.ClearDomainEvents();
        return d;
    }

    private static IDriverReadService GetReadService()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDriverReadService>();
    }

    // ── Status derivation ─────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 60)]
    public async Task GetAllAsync_Returns_Available_When_Driver_Has_No_Active_Deliveries()
    {
        await ResetDatabaseAsync();

        var driver = MakeDriver();
        await SeedDriversAsync([driver]);

        var result = await GetReadService().GetAllAsync(null, null, null, 10, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be("available");
        result.Items[0].Name.Should().Be("Ahmad Hassan");
        result.Items[0].ShortName.Should().Be("Ahmad");
    }

    [Test]
    [NotInParallel(Order = 61)]
    public async Task GetAllAsync_Returns_InTransit_When_Driver_Has_Active_InTransit_Delivery()
    {
        await ResetDatabaseAsync();

        var driver = MakeDriver();
        var delivery = MakeDelivery(driver.DriverId, driver.DistrictId, DeliveryStatus.InTransit);
        await SeedDriversAsync([driver]);
        await SeedDeliveriesAsync([delivery]);

        var result = await GetReadService().GetAllAsync(null, null, null, 10, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be("in-transit");
        result.Items[0].ActiveDeliveries.Should().Be(1);
    }

    [Test]
    [NotInParallel(Order = 62)]
    public async Task GetAllAsync_Returns_Stalled_When_Driver_Has_Anomalous_Delivery()
    {
        await ResetDatabaseAsync();

        var driver = MakeDriver();
        var delivery = MakeDelivery(driver.DriverId, driver.DistrictId, DeliveryStatus.Anomalous, anomaly: true);
        await SeedDriversAsync([driver]);
        await SeedDeliveriesAsync([delivery]);

        var result = await GetReadService().GetAllAsync(null, null, null, 10, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be("stalled");
        result.Items[0].HasAnomaly.Should().BeTrue();
        result.Items[0].AnomalyReason.Should().NotBeNullOrEmpty();
    }

    [Test]
    [NotInParallel(Order = 63)]
    public async Task GetAllAsync_Returns_Offline_When_Driver_IsActive_False()
    {
        await ResetDatabaseAsync();

        var driver = MakeDriver(isActive: false);
        await SeedDriversAsync([driver]);

        var result = await GetReadService().GetAllAsync(null, null, null, 10, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be("offline");
    }

    // ── Filtering ─────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 64)]
    public async Task GetAllAsync_Filters_By_DistrictId()
    {
        await ResetDatabaseAsync();

        var a = MakeDriver(district: "mezzeh");
        var b = MakeDriver(name: "Sami Karimi", shortName: "Sami", district: "malki");
        await SeedDriversAsync([a, b]);

        var result = await GetReadService().GetAllAsync(null, "mezzeh", null, 10, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].DistrictId.Should().Be("mezzeh");
    }

    [Test]
    [NotInParallel(Order = 65)]
    public async Task GetAllAsync_Filters_By_Status_Offline()
    {
        await ResetDatabaseAsync();

        var online  = MakeDriver(name: "Online Driver", shortName: "OD", isActive: true);
        var offline = MakeDriver(name: "Offline Driver", shortName: "FD", isActive: false);
        await SeedDriversAsync([online, offline]);

        var result = await GetReadService().GetAllAsync(null, null, "offline", 10, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be("offline");
    }

    // ── Pagination ────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 66)]
    public async Task GetAllAsync_Paginates_And_Returns_NextCursor()
    {
        await ResetDatabaseAsync();

        var drivers = Enumerable.Range(1, 5)
            .Select(i => MakeDriver(name: $"Driver {i}", shortName: $"D{i}"))
            .ToList();
        await SeedDriversAsync(drivers);

        var page1 = await GetReadService().GetAllAsync(null, null, null, 3, CancellationToken.None);

        page1.Items.Should().HaveCount(3);
        page1.NextCursor.Should().NotBeNull();

        var page2 = await GetReadService().GetAllAsync(page1.NextCursor, null, null, 3, CancellationToken.None);

        page2.Items.Should().HaveCount(2);
        page2.NextCursor.Should().BeNull();
    }

    // ── VehicleAndContactInfo ─────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 68)]
    public async Task GetAllAsync_Returns_CarType_LicensePlate_PhoneNumber_When_Set()
    {
        await ResetDatabaseAsync();

        var d = Driver.Create(
            Guid.NewGuid(), Damascus, "mezzeh", DateTime.UtcNow,
            "Ahmad Hassan", "Ahmad", true,
            carType: "Sedan", licensePlate: "AHM-9901", phoneNumber: "+963-911-990001").Value;
        d.ClearDomainEvents();
        await SeedDriversAsync([d]);

        var result = await GetReadService().GetAllAsync(null, null, null, 10, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].CarType.Should().Be("Sedan");
        result.Items[0].LicensePlate.Should().Be("AHM-9901");
        result.Items[0].PhoneNumber.Should().Be("+963-911-990001");
    }

    // ── CompletedToday ────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 67)]
    public async Task GetAllAsync_Counts_CompletedToday_Correctly()
    {
        await ResetDatabaseAsync();

        var driver = MakeDriver();
        var delivered = MakeDelivery(driver.DriverId, driver.DistrictId, DeliveryStatus.Delivered);
        await SeedDriversAsync([driver]);
        await SeedDeliveriesAsync([delivered]);

        var result = await GetReadService().GetAllAsync(null, null, null, 10, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].CompletedToday.Should().Be(1);
        result.Items[0].ActiveDeliveries.Should().Be(0);
    }
}
