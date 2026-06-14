using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Analytics;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Domain.ValueObjects;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class DriverAnalyticsIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static Point Location => Geo.CreatePoint(new Coordinate(36.27, 33.51));

    private static Driver MakeDriver(string district = "mezzeh")
    {
        var r = Driver.Create(Guid.NewGuid(), Location, district, DateTime.UtcNow, "Test Driver", "TD", true);
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    // Delivery that is delivered; onTime = deliveredAt <= expectedEta.
    private static Delivery MakeDelivered(Guid driverId, string district, bool onTime)
    {
        var now       = DateTime.UtcNow;
        var pickedUp  = now.AddHours(-5);
        var delivered = pickedUp.AddMinutes(60);                         // 60 min actual
        var eta       = onTime ? pickedUp.AddMinutes(90) : pickedUp.AddMinutes(45); // 90 or 45 min

        var d = Delivery.Create(Guid.NewGuid(), Location, district, pickedUp.AddMinutes(-5), eta).Value;
        d.AssignDriver(driverId);
        d.MarkPickedUp(Location, pickedUp);
        d.MarkDelivered(delivered);
        d.ClearDomainEvents();
        return d;
    }

    private static Delivery MakeAnomalous(Guid driverId, string district)
    {
        var d = Delivery.Create(Guid.NewGuid(), Location, district, DateTime.UtcNow.AddHours(-2), null).Value;
        d.AssignDriver(driverId);
        d.FlagAnomaly(AnomalyType.EtaExceeded, "Flagged in test");
        d.ClearDomainEvents();
        return d;
    }

    [Test]
    [NotInParallel(Order = 200)]
    public async Task GetDriverAnalytics_Returns_All_Drivers_Including_Idle()
    {
        await ResetDatabaseAsync();

        var driverA = MakeDriver("mezzeh");
        var driverB = MakeDriver("malki");     // no deliveries

        await SeedDriversAsync([driverA, driverB]);

        var del1 = MakeDelivered(driverA.DriverId, "mezzeh", onTime: true);
        await SeedDeliveriesAsync([del1]);

        var result = await InvokeAsync<GetDriverAnalyticsResponse>(new GetDriverAnalyticsQuery());

        result.Drivers.Should().HaveCount(2);
        var idle = result.Drivers.First(d => d.DriverId == driverB.DriverId);
        idle.TotalLast7Days.Should().Be(0);
        idle.OnTimeRatePct.Should().BeNull();
        idle.AnomalyRate.Should().Be(0.0);
    }

    [Test]
    [NotInParallel(Order = 201)]
    public async Task GetDriverAnalytics_Computes_OnTimeRate_And_AnomalyRate_Correctly()
    {
        await ResetDatabaseAsync();

        var driver = MakeDriver("mezzeh");
        await SeedDriversAsync([driver]);

        // 1 on-time, 1 late, 1 anomalous → 3 total assigned deliveries
        var onTime    = MakeDelivered(driver.DriverId, "mezzeh", onTime: true);
        var late      = MakeDelivered(driver.DriverId, "mezzeh", onTime: false);
        var anomalous = MakeAnomalous(driver.DriverId, "mezzeh");
        await SeedDeliveriesAsync([onTime, late, anomalous]);

        var result = await InvokeAsync<GetDriverAnalyticsResponse>(new GetDriverAnalyticsQuery());

        var stats = result.Drivers.Single(d => d.DriverId == driver.DriverId);

        stats.TotalLast7Days.Should().Be(3);
        stats.CompletedLast7Days.Should().Be(2);     // onTime + late
        stats.OnTimeRatePct.Should().BeApproximately(0.5, 0.001);   // 1 of 2 completed on time
        stats.AnomalyRate.Should().BeApproximately(1.0 / 3.0, 0.001); // 1 of 3 total
        stats.AvgDurationSeconds.Should().BeApproximately(3600.0, 5.0); // both 60 min = 3600 s
    }

    [Test]
    [NotInParallel(Order = 202)]
    public async Task GetDriverAnalytics_Computes_DistrictAvgDuration_For_Comparison()
    {
        await ResetDatabaseAsync();

        var driver = MakeDriver("kafrsousa");
        await SeedDriversAsync([driver]);

        var d1 = MakeDelivered(driver.DriverId, "kafrsousa", onTime: true);
        var d2 = MakeDelivered(driver.DriverId, "kafrsousa", onTime: false);
        await SeedDeliveriesAsync([d1, d2]);

        var result = await InvokeAsync<GetDriverAnalyticsResponse>(new GetDriverAnalyticsQuery());

        var stats = result.Drivers.Single(d => d.DriverId == driver.DriverId);

        // Both deliveries in same district so driver avg == district avg
        stats.DistrictAvgDurationSeconds.Should().BeApproximately(stats.AvgDurationSeconds, 1.0);
    }

    [Test]
    [NotInParallel(Order = 203)]
    public async Task GetDriverAnalytics_Includes_HourlyOnTimeBreakdown_When_Data_Exists()
    {
        await ResetDatabaseAsync();

        var driver = MakeDriver("babtouma");
        await SeedDriversAsync([driver]);

        var del = MakeDelivered(driver.DriverId, "babtouma", onTime: true);
        await SeedDeliveriesAsync([del]);

        var result = await InvokeAsync<GetDriverAnalyticsResponse>(new GetDriverAnalyticsQuery());

        var stats = result.Drivers.Single(d => d.DriverId == driver.DriverId);

        // At least one hourly bucket should be present
        stats.OnTimeByHour.Should().NotBeEmpty();
        stats.OnTimeByHour.Should().AllSatisfy(p =>
        {
            p.Hour.Should().BeInRange(0, 23);
            p.SampleCount.Should().BeGreaterThan(0);
            p.OnTimeRatePct.Should().BeInRange(0.0, 1.0);
        });
    }
}
