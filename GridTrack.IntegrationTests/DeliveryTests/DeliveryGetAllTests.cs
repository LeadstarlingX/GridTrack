using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Deliveries;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DeliveryTests;

public class DeliveryGetAllTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static Delivery CreateDelivery(
        string districtId = "h3-getall",
        DateTime? createdAt = null,
        DateTime? eta = null)
    {
        var r = Delivery.Create(Guid.NewGuid(), Damascus, districtId, createdAt ?? DateTime.UtcNow, eta);
        r.IsSuccess.Should().BeTrue();
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    [Test]
    [NotInParallel(Order = 130)]
    public async Task GetDeliveriesQuery_Returns_Empty_When_No_Deliveries()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(null, null, null, null, null, 10));

        result.Items.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 131)]
    public async Task GetDeliveriesQuery_Returns_Paginated_Results()
    {
        await ResetDatabaseAsync();

        var deliveries = Enumerable.Range(0, 5)
            .Select(i => CreateDelivery(createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();
        await SeedDeliveriesAsync(deliveries);

        var page1 = await InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(null, null, null, null, null, 3));

        page1.Items.Should().HaveCount(3);
        page1.NextCursor.Should().NotBeNull();

        var page2 = await InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(page1.NextCursor, null, null, null, null, 3));

        page2.Items.Should().HaveCount(2);
        page2.NextCursor.Should().BeNull();

        var page1Ids = page1.Items.Select(d => d.Id).ToHashSet();
        var page2Ids = page2.Items.Select(d => d.Id).ToHashSet();
        page1Ids.Should().NotIntersectWith(page2Ids);
    }

    [Test]
    [NotInParallel(Order = 132)]
    public async Task GetDeliveriesQuery_Filters_By_DistrictId()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([
            CreateDelivery(districtId: "h3-target-a"),
            CreateDelivery(districtId: "h3-target-a"),
            CreateDelivery(districtId: "h3-other-b"),
        ]);

        var result = await InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(null, null, "h3-target-a", null, null, 10));

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(d => d.DistrictId == "h3-target-a");
    }

    [Test]
    [NotInParallel(Order = 133)]
    public async Task GetDeliveriesQuery_Filters_By_Status()
    {
        await ResetDatabaseAsync();

        var created = CreateDelivery(districtId: "h3-status-filter");
        var assigned = CreateDelivery(districtId: "h3-status-filter");
        assigned.AssignDriver(Guid.NewGuid());
        assigned.ClearDomainEvents();
        await SeedDeliveriesAsync([created, assigned]);

        var result = await InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(null, "assigned", null, null, null, 10));

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be("Assigned");
    }

    [Test]
    [NotInParallel(Order = 134)]
    public async Task GetDeliveriesQuery_Filters_By_DateRange()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        await SeedDeliveriesAsync([
            CreateDelivery(createdAt: now.AddDays(-10)),
            CreateDelivery(createdAt: now.AddDays(-1)),
            CreateDelivery(createdAt: now),
        ]);

        var result = await InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(null, null, null, now.AddDays(-2), now.AddDays(1), 10));

        result.Items.Should().HaveCount(2);
    }

    [Test]
    [NotInParallel(Order = 135)]
    public async Task GetDeliveriesQuery_Items_Are_Ordered_Newest_First()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        var old = CreateDelivery(createdAt: now.AddMinutes(-5));
        var mid = CreateDelivery(createdAt: now.AddMinutes(-2));
        var newest = CreateDelivery(createdAt: now);
        await SeedDeliveriesAsync([old, mid, newest]);

        var result = await InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(null, null, null, null, null, 10));

        result.Items.Should().HaveCount(3);
        result.Items[0].Id.Should().Be(newest.DeliveryId);
        result.Items[2].Id.Should().Be(old.DeliveryId);
    }

    [Test]
    [NotInParallel(Order = 136)]
    public async Task GetDeliveriesQuery_Includes_AssignedDriverName_When_Driver_Exists()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery(districtId: "h3-driver-name");
        var driver = Domain.Drivers.Driver.Create(
            Guid.NewGuid(), Damascus, "h3-driver-name",
            DateTime.UtcNow, "Khalil Nasser", "Khalil", true).Value;
        driver.ClearDomainEvents();
        delivery.AssignDriver(driver.DriverId);
        delivery.ClearDomainEvents();

        await SeedDriversAsync([driver]);
        await SeedDeliveriesAsync([delivery]);

        var result = await InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(null, null, "h3-driver-name", null, null, 10));

        result.Items.Should().HaveCount(1);
        result.Items[0].AssignedDriverName.Should().Be("Khalil Nasser");
    }
}
