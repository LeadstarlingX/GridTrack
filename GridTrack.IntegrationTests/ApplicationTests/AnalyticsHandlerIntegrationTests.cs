using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Analytics;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Domain.ValueObjects;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class AnalyticsHandlerIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static Delivery CreateDelivery(string districtId = "h3-analytics", DateTime? createdAt = null)
    {
        var r = Delivery.Create(Guid.NewGuid(), Damascus, districtId, createdAt ?? DateTime.UtcNow, null);
        r.IsSuccess.Should().BeTrue();
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    private static Driver CreateDriver(bool isActive = true)
    {
        var r = Driver.Create(Guid.NewGuid(), Damascus, "h3-analytics", DateTime.UtcNow, isActive);
        r.IsSuccess.Should().BeTrue();
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    // ── GetAnalyticsSummaryQuery ──────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 500)]
    public async Task GetAnalyticsSummaryQuery_Should_Return_Zero_Counts_For_Empty_Database()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<GetAnalyticsSummaryResponse>(new GetAnalyticsSummaryQuery());

        result.Should().NotBeNull();
        result.TotalDeliveriesToday.Should().Be(0);
        result.ActiveDrivers.Should().Be(0);
        result.CompletionRate.Should().Be(0.0);
        result.AnomalyRate.Should().Be(0.0);
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Test]
    [NotInParallel(Order = 501)]
    public async Task GetAnalyticsSummaryQuery_Should_Count_Active_Drivers()
    {
        await ResetDatabaseAsync();

        await SeedDriversAsync([CreateDriver(isActive: true), CreateDriver(isActive: true), CreateDriver(isActive: false)]);

        var result = await InvokeAsync<GetAnalyticsSummaryResponse>(new GetAnalyticsSummaryQuery());

        result.ActiveDrivers.Should().Be(2);
    }

    [Test]
    [NotInParallel(Order = 502)]
    public async Task GetAnalyticsSummaryQuery_Should_Count_Deliveries_Created_Today()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([CreateDelivery(), CreateDelivery(), CreateDelivery()]);

        var result = await InvokeAsync<GetAnalyticsSummaryResponse>(new GetAnalyticsSummaryQuery());

        result.TotalDeliveriesToday.Should().Be(3);
    }

    [Test]
    [NotInParallel(Order = 503)]
    public async Task GetAnalyticsSummaryQuery_Should_Calculate_Anomaly_Rate()
    {
        await ResetDatabaseAsync();

        var normal = CreateDelivery();
        var anomalous = CreateDelivery();
        anomalous.AssignDriver(Guid.NewGuid());
        var pt = GeoFactory.CreatePoint(new Coordinate(36.28, 33.52));
        anomalous.MarkPickedUp(pt, DateTime.UtcNow);
        anomalous.FlagAnomaly(AnomalyType.EtaExceeded, "Late");
        anomalous.ClearDomainEvents();

        await SeedDeliveriesAsync([normal, anomalous]);

        var result = await InvokeAsync<GetAnalyticsSummaryResponse>(new GetAnalyticsSummaryQuery());

        result.AnomalyRate.Should().BeApproximately(0.5, precision: 0.001);
    }

    // ── GetH3DensityQuery ─────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 510)]
    public async Task GetH3DensityQuery_Should_Return_Empty_When_No_Deliveries()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<GetH3DensityResponse>(
            new GetH3DensityQuery(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 9, null, null));

        result.Should().NotBeNull();
        result.Cells.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 511)]
    public async Task GetH3DensityQuery_Should_Group_By_District_And_Count()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([
            CreateDelivery("h3-zone-a"),
            CreateDelivery("h3-zone-a"),
            CreateDelivery("h3-zone-a"),
            CreateDelivery("h3-zone-b"),
            CreateDelivery("h3-zone-b"),
        ]);

        var result = await InvokeAsync<GetH3DensityResponse>(
            new GetH3DensityQuery(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), 9, null, null));

        result.Cells.Should().HaveCount(2);
        var zoneA = result.Cells.First(c => c.H3Index == "h3-zone-a");
        var zoneB = result.Cells.First(c => c.H3Index == "h3-zone-b");
        zoneA.DeliveryCount.Should().Be(3);
        zoneB.DeliveryCount.Should().Be(2);
    }

    [Test]
    [NotInParallel(Order = 512)]
    public async Task GetH3DensityQuery_Should_Filter_By_Time_Window()
    {
        await ResetDatabaseAsync();

        var past = CreateDelivery("h3-zone-x", DateTime.UtcNow.AddDays(-5));
        var recent = CreateDelivery("h3-zone-x", DateTime.UtcNow.AddHours(-1));
        await SeedDeliveriesAsync([past, recent]);

        var result = await InvokeAsync<GetH3DensityResponse>(
            new GetH3DensityQuery(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 9, null, null));

        result.Cells.Should().HaveCount(1);
        result.Cells[0].H3Index.Should().Be("h3-zone-x");
        result.Cells[0].DeliveryCount.Should().Be(1);
    }

    // ── GetTrendsQuery ────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 520)]
    public async Task GetTrendsQuery_Should_Return_Empty_Trends_For_Empty_Database()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<GetTrendsResponse>(
            new GetTrendsQuery(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, "day"));

        result.Should().NotBeNull();
        result.DeliveryTrend.Should().BeEmpty();
        result.AnomalyTrend.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 521)]
    public async Task GetTrendsQuery_Should_Return_Delivery_Counts_By_Granularity()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([CreateDelivery(), CreateDelivery(), CreateDelivery()]);

        var result = await InvokeAsync<GetTrendsResponse>(
            new GetTrendsQuery(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), "day"));

        result.DeliveryTrend.Should().HaveCount(1);
        result.DeliveryTrend[0].Value.Should().Be(3.0);
    }

    [Test]
    [NotInParallel(Order = 522)]
    public async Task GetTrendsQuery_Should_Separate_Anomaly_Trend()
    {
        await ResetDatabaseAsync();

        var normal = CreateDelivery();
        var anomalous = CreateDelivery();
        anomalous.AssignDriver(Guid.NewGuid());
        var pt = GeoFactory.CreatePoint(new Coordinate(36.28, 33.52));
        anomalous.MarkPickedUp(pt, DateTime.UtcNow);
        anomalous.FlagAnomaly(AnomalyType.RouteDeviation, "Off-route");
        anomalous.ClearDomainEvents();

        await SeedDeliveriesAsync([normal, anomalous]);

        var result = await InvokeAsync<GetTrendsResponse>(
            new GetTrendsQuery(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), "day"));

        result.DeliveryTrend[0].Value.Should().Be(2.0);
        result.AnomalyTrend[0].Value.Should().Be(1.0);
    }
}
