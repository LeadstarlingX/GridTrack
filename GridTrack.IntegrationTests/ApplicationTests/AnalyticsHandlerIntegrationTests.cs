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
        var r = Driver.Create(Guid.NewGuid(), Damascus, "h3-analytics", DateTime.UtcNow, "Ahmad Hassan", "Ahmad", isActive);
        r.IsSuccess.Should().BeTrue();
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    private static Delivery CreateCancelledDelivery(string districtId, bool late, string reason)
    {
        var createdAt = DateTime.UtcNow.AddHours(-1);
        var expectedEta = late ? createdAt.AddMinutes(20) : createdAt.AddHours(3);
        var cancelAt = late ? expectedEta.AddMinutes(10) : createdAt.AddMinutes(5);
        var d = Delivery.Create(Guid.NewGuid(), Damascus, districtId, createdAt, expectedEta).Value;
        d.MarkCancelled(cancelAt, reason).IsSuccess.Should().BeTrue();
        d.ClearDomainEvents();
        return d;
    }

    private static Delivery CreateDeliveredDelivery(string districtId, Guid driverId, DateTime pickedUpAt, DateTime deliveredAt, DateTime expectedEta)
    {
        var d = Delivery.Create(Guid.NewGuid(), Damascus, districtId, pickedUpAt.AddMinutes(-5), expectedEta).Value;
        d.AssignDriver(driverId);
        d.MarkPickedUp(Damascus, pickedUpAt);
        d.MarkDelivered(deliveredAt);
        d.ClearDomainEvents();
        return d;
    }

    private static Delivery CreateInTransitDelivery(string districtId, Guid driverId)
    {
        var d = Delivery.Create(Guid.NewGuid(), Damascus, districtId, DateTime.UtcNow.AddMinutes(-30), null).Value;
        d.AssignDriver(driverId);
        d.MarkPickedUp(Damascus, DateTime.UtcNow.AddMinutes(-20));
        d.UpdateLocation(Damascus, DateTime.UtcNow.AddMinutes(-10)); // → InTransit
        d.ClearDomainEvents();
        return d;
    }

    private static Delivery CreateAnomalousDelivery(string districtId, AnomalyType type)
    {
        var d = Delivery.Create(Guid.NewGuid(), Damascus, districtId, DateTime.UtcNow, null).Value;
        d.FlagAnomaly(type, $"{type} detected").IsSuccess.Should().BeTrue();
        d.ClearDomainEvents();
        return d;
    }

    private static Driver CreateDriverWithId(Guid id, bool isActive)
    {
        var r = Driver.Create(id, Damascus, "h3-analytics", DateTime.UtcNow, "Ahmad Hassan", "Ahmad", isActive);
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

    // ── GetDistrictVolumeQuery ────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 530)]
    public async Task GetDistrictVolumeQuery_Should_Group_And_Order_By_Count_Desc()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([
            CreateDelivery("mezzeh"),
            CreateDelivery("mezzeh"),
            CreateDelivery("mezzeh"),
            CreateDelivery("kafrsousa"),
            CreateDelivery("kafrsousa"),
        ]);

        var result = await InvokeAsync<GetDistrictVolumeResponse>(new GetDistrictVolumeQuery(null, null));

        result.Items.Should().HaveCount(2);
        result.Items[0].DistrictId.Should().Be("mezzeh");
        result.Items[0].Deliveries.Should().Be(3);
        result.Items[1].DistrictId.Should().Be("kafrsousa");
        result.Items[1].Deliveries.Should().Be(2);
    }

    // ── GetCancellationAnalyticsQuery ─────────────────────────────────────

    [Test]
    [NotInParallel(Order = 540)]
    public async Task GetCancellationAnalyticsQuery_Should_Count_Cancellations_And_Late_Ones()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([
            CreateDelivery("mezzeh"),                                                   // not cancelled
            CreateDelivery("mezzeh"),                                                   // not cancelled
            CreateCancelledDelivery("mezzeh", late: true,  reason: "client unreachable"),
            CreateCancelledDelivery("kafrsousa", late: false, reason: "client request"),
        ]);

        var result = await InvokeAsync<GetCancellationAnalyticsResponse>(
            new GetCancellationAnalyticsQuery(null, null));

        result.TotalCancelled.Should().Be(2);
        result.LateCancellations.Should().Be(1);
        result.CancellationRate.Should().BeApproximately(0.5, precision: 0.001);
        result.Reasons.Should().HaveCount(2);
        result.Reasons.Sum(r => r.Count).Should().Be(2);
    }

    [Test]
    [NotInParallel(Order = 541)]
    public async Task GetCancellationAnalyticsQuery_Should_Return_Zeroes_For_Empty_Database()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<GetCancellationAnalyticsResponse>(
            new GetCancellationAnalyticsQuery(null, null));

        result.TotalCancelled.Should().Be(0);
        result.LateCancellations.Should().Be(0);
        result.CancellationRate.Should().Be(0.0);
        result.Reasons.Should().BeEmpty();
    }

    // ── GetDeliveryPerformanceQuery ───────────────────────────────────────

    [Test]
    [NotInParallel(Order = 550)]
    public async Task GetDeliveryPerformanceQuery_Should_Compute_Duration_And_OnTime_Rate()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        // On-time: delivered before ETA; actual duration 30 min.
        var onTime = CreateDeliveredDelivery("mezzeh", Guid.NewGuid(),
            pickedUpAt: now.AddMinutes(-50), deliveredAt: now.AddMinutes(-20), expectedEta: now.AddMinutes(-10));
        // Late: delivered after ETA; actual duration 40 min.
        var late = CreateDeliveredDelivery("mezzeh", Guid.NewGuid(),
            pickedUpAt: now.AddMinutes(-50), deliveredAt: now.AddMinutes(-10), expectedEta: now.AddMinutes(-30));
        await SeedDeliveriesAsync([onTime, late]);

        var result = await InvokeAsync<GetDeliveryPerformanceResponse>(
            new GetDeliveryPerformanceQuery(null, null));

        result.DeliveredCount.Should().Be(2);
        result.OverallOnTimeRate.Should().BeApproximately(0.5, precision: 0.001);
        result.OverallAvgDurationSeconds.Should().BeGreaterThan(0);
        result.Districts.Should().ContainSingle(d => d.DistrictId == "mezzeh");
        result.Districts[0].DeliveredCount.Should().Be(2);
        result.Districts[0].OnTimeRate.Should().BeApproximately(0.5, precision: 0.001);
    }

    // ── GetDriverUtilizationQuery ─────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 560)]
    public async Task GetDriverUtilizationQuery_Should_Count_Drivers_And_Throughput()
    {
        await ResetDatabaseAsync();

        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        await SeedDriversAsync([CreateDriverWithId(activeId, true), CreateDriverWithId(inactiveId, false)]);

        var now = DateTime.UtcNow;
        await SeedDeliveriesAsync([
            CreateDeliveredDelivery("h3-analytics", activeId, now.AddMinutes(-40), now.AddMinutes(-10), now.AddMinutes(-5)),
            CreateInTransitDelivery("h3-analytics", activeId),
        ]);

        var result = await InvokeAsync<GetDriverUtilizationResponse>(new GetDriverUtilizationQuery(10));

        result.ActiveDrivers.Should().Be(1);
        result.InactiveDrivers.Should().Be(1);
        result.AvgActiveDeliveriesPerActiveDriver.Should().BeApproximately(1.0, precision: 0.001);
        var topActive = result.TopDrivers.First(d => d.DriverId == activeId);
        topActive.CompletedToday.Should().Be(1);
        topActive.ActiveDeliveries.Should().Be(1);
    }

    // ── GetAnomalyBreakdownQuery ──────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 570)]
    public async Task GetAnomalyBreakdownQuery_Should_Group_By_Type_And_District()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([
            CreateAnomalousDelivery("mezzeh", AnomalyType.EtaExceeded),
            CreateAnomalousDelivery("mezzeh", AnomalyType.EtaExceeded),
            CreateAnomalousDelivery("kafrsousa", AnomalyType.RouteDeviation),
        ]);

        var result = await InvokeAsync<GetAnomalyBreakdownResponse>(new GetAnomalyBreakdownQuery(null, null));

        result.ByType.First(t => t.AnomalyType == AnomalyType.EtaExceeded).Count.Should().Be(2);
        result.ByType.First(t => t.AnomalyType == AnomalyType.RouteDeviation).Count.Should().Be(1);
        result.ByDistrict.First(d => d.DistrictId == "mezzeh").Count.Should().Be(2);
        result.ByDistrict.First(d => d.DistrictId == "kafrsousa").Count.Should().Be(1);
    }
}
