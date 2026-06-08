using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Alerts;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class AlertsPaginationIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static Delivery CreateAnomalousDelivery(string districtId = "h3-alerts", DateTime? createdAt = null)
    {
        var r = Delivery.Create(Guid.NewGuid(), Damascus, districtId, createdAt ?? DateTime.UtcNow, null);
        r.IsSuccess.Should().BeTrue();
        var delivery = r.Value;
        delivery.AssignDriver(Guid.NewGuid());
        var pt = GeoFactory.CreatePoint(new Coordinate(36.28, 33.52));
        delivery.MarkPickedUp(pt, DateTime.UtcNow);
        delivery.FlagAnomaly(AnomalyType.EtaExceeded, "Late delivery");
        delivery.ClearDomainEvents();
        return delivery;
    }

    [Test]
    [NotInParallel(Order = 700)]
    public async Task GetAlertsQuery_Should_Return_Empty_When_No_Anomalies()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<GetAlertsResponse>(
            new GetAlertsQuery(null, null, null, null, null, PageSize: 10));

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 701)]
    public async Task GetAlertsQuery_Should_Return_Anomalous_Deliveries()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([
            CreateAnomalousDelivery(),
            CreateAnomalousDelivery(),
        ]);

        var result = await InvokeAsync<GetAlertsResponse>(
            new GetAlertsQuery(null, null, null, null, null, PageSize: 10));

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(a => a.Id != Guid.Empty);
        result.Items.Should().OnlyContain(a => a.AnomalyType == "Delay");
    }

    [Test]
    [NotInParallel(Order = 702)]
    public async Task GetAlertsQuery_Should_Filter_By_DistrictId()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([
            CreateAnomalousDelivery(districtId: "h3-target"),
            CreateAnomalousDelivery(districtId: "h3-target"),
            CreateAnomalousDelivery(districtId: "h3-other"),
        ]);

        var result = await InvokeAsync<GetAlertsResponse>(
            new GetAlertsQuery(null, null, null, "h3-target", null, PageSize: 10));

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(a => a.DistrictId == "h3-target");
    }

    [Test]
    [NotInParallel(Order = 703)]
    public async Task GetAlertsQuery_Should_Paginate_And_Return_NextCursor()
    {
        await ResetDatabaseAsync();

        var deliveries = Enumerable.Range(0, 15)
            .Select(i => CreateAnomalousDelivery(createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        await SeedDeliveriesAsync(deliveries);

        // First page
        var page1 = await InvokeAsync<GetAlertsResponse>(
            new GetAlertsQuery(null, null, null, null, null, PageSize: 10));

        page1.Items.Should().HaveCount(10);
        page1.NextCursor.Should().NotBeNullOrEmpty();

        // Second page using cursor
        var page2 = await InvokeAsync<GetAlertsResponse>(
            new GetAlertsQuery(page1.NextCursor, null, null, null, null, PageSize: 10));

        page2.Items.Should().HaveCount(5);
        page2.NextCursor.Should().BeNull();

        // No overlap between pages
        var page1Ids = page1.Items.Select(a => a.Id).ToHashSet();
        var page2Ids = page2.Items.Select(a => a.Id).ToHashSet();
        page1Ids.Should().NotIntersectWith(page2Ids);
    }

    [Test]
    [NotInParallel(Order = 704)]
    public async Task GetAlertsQuery_Should_Filter_By_DateRange()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        await SeedDeliveriesAsync([
            CreateAnomalousDelivery(createdAt: now.AddDays(-10)), // outside range
            CreateAnomalousDelivery(createdAt: now.AddDays(-1)),  // inside range
            CreateAnomalousDelivery(createdAt: now),              // inside range
        ]);

        var result = await InvokeAsync<GetAlertsResponse>(
            new GetAlertsQuery(null, now.AddDays(-2), now.AddDays(1), null, null, PageSize: 10));

        result.Items.Should().HaveCount(2);
    }
}
