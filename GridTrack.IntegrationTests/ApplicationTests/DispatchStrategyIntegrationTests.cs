using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Dispatch;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class DispatchStrategyIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);

    // Damascus city centre — query origin
    private static Point Damascus => Geo.CreatePoint(new Coordinate(36.2765, 33.5138));

    // ~300 km north — used to make a clearly distant driver
    private static Point Aleppo => Geo.CreatePoint(new Coordinate(37.1612, 36.2021));

    private static Driver MakeDriver(
        Point location,
        string district    = "mezzeh",
        DateTime? shiftStart = null,
        DateTime? shiftEnd   = null)
    {
        var r = Driver.Create(
            Guid.NewGuid(), location, district, DateTime.UtcNow,
            "Test Driver", "TD", isActive: true,
            shiftStartedAt: shiftStart, shiftEndsAt: shiftEnd);
        r.IsSuccess.Should().BeTrue();
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    // Completed delivery with optional ETA (null ETA → excluded from on-time calc)
    private static Delivery MakeDelivered(Guid driverId, bool onTime)
    {
        var now      = DateTime.UtcNow;
        var pickedUp = now.AddHours(-2);
        var delivered = pickedUp.AddMinutes(60);
        // onTime: delivered before ETA; late: delivered after ETA
        var eta      = onTime ? pickedUp.AddMinutes(90) : pickedUp.AddMinutes(45);

        var d = Delivery.Create(Guid.NewGuid(), Damascus, "mezzeh", pickedUp.AddMinutes(-5), eta).Value;
        d.AssignDriver(driverId);
        d.MarkPickedUp(Damascus, pickedUp);
        d.MarkDelivered(delivered);
        d.ClearDomainEvents();
        return d;
    }

    // In-transit delivery (counts as active load)
    private static Delivery MakeActive(Guid driverId)
    {
        var d = Delivery.Create(Guid.NewGuid(), Damascus, "mezzeh", DateTime.UtcNow.AddHours(-1), null).Value;
        d.AssignDriver(driverId);
        d.MarkPickedUp(Damascus, DateTime.UtcNow.AddMinutes(-30));
        d.UpdateLocation(Damascus, DateTime.UtcNow.AddMinutes(-10));
        d.ClearDomainEvents();
        return d;
    }

    [Test]
    [NotInParallel(Order = 300)]
    public async Task GetDispatchCandidates_Returns_Empty_When_No_Active_Drivers()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<IReadOnlyList<DispatchCandidateDto>>(
            new GetDispatchCandidatesQuery(Damascus, 5));

        result.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 301)]
    public async Task GetDispatchCandidates_Scores_Closer_Driver_Higher()
    {
        await ResetDatabaseAsync();

        var near = MakeDriver(Damascus);   // at query origin → distance ≈ 0
        var far  = MakeDriver(Aleppo);    // ~300 km away

        await SeedDriversAsync([near, far]);

        var result = await InvokeAsync<IReadOnlyList<DispatchCandidateDto>>(
            new GetDispatchCandidatesQuery(Damascus, 5));

        result.Should().HaveCount(2);
        var nearResult = result.First(d => d.DriverId == near.DriverId);
        var farResult  = result.First(d => d.DriverId == far.DriverId);
        nearResult.Score.Should().BeGreaterThan(farResult.Score);
    }

    [Test]
    [NotInParallel(Order = 302)]
    public async Task GetDispatchCandidates_Scores_HighOnTime_Driver_Higher_Than_LowOnTime()
    {
        await ResetDatabaseAsync();

        // Both drivers at the same location so proximity scores are equal.
        var goodDriver = MakeDriver(Damascus, "mezzeh");
        var poorDriver = MakeDriver(Damascus, "mezzeh");
        await SeedDriversAsync([goodDriver, poorDriver]);

        // goodDriver: 1 on-time delivery → OnTimeRatePct = 1.0
        // poorDriver: 1 late delivery    → OnTimeRatePct = 0.0
        var onTime = MakeDelivered(goodDriver.DriverId, onTime: true);
        var late   = MakeDelivered(poorDriver.DriverId, onTime: false);
        await SeedDeliveriesAsync([onTime, late]);

        var result = await InvokeAsync<IReadOnlyList<DispatchCandidateDto>>(
            new GetDispatchCandidatesQuery(Damascus, 5));

        result.Should().HaveCount(2);
        var good = result.First(d => d.DriverId == goodDriver.DriverId);
        var poor = result.First(d => d.DriverId == poorDriver.DriverId);
        good.OnTimeRatePct.Should().Be(1.0);
        poor.OnTimeRatePct.Should().Be(0.0);
        good.Score.Should().BeGreaterThan(poor.Score);
    }

    [Test]
    [NotInParallel(Order = 303)]
    public async Task GetDispatchCandidates_Scores_ShiftActive_Driver_Higher_Than_OffShift()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;

        // inShift: shift covers now
        var inShift  = MakeDriver(Damascus, "mezzeh",
            shiftStart: now.AddHours(-2), shiftEnd: now.AddHours(6));
        // offShift: shift ended yesterday
        var offShift = MakeDriver(Damascus, "mezzeh",
            shiftStart: now.AddDays(-1).AddHours(-8), shiftEnd: now.AddDays(-1));

        await SeedDriversAsync([inShift, offShift]);

        var result = await InvokeAsync<IReadOnlyList<DispatchCandidateDto>>(
            new GetDispatchCandidatesQuery(Damascus, 5));

        result.Should().HaveCount(2);
        var active   = result.First(d => d.DriverId == inShift.DriverId);
        var inactive = result.First(d => d.DriverId == offShift.DriverId);
        active.ShiftScore.Should().Be(1.0);
        inactive.ShiftScore.Should().Be(0.0);
        active.Score.Should().BeGreaterThan(inactive.Score);
    }
}
