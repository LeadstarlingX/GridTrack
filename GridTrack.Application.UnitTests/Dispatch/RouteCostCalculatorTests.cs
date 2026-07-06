using GridTrack.Application.Dispatch;
using Microsoft.Extensions.Options;

namespace GridTrack.Application.UnitTests.Dispatch;

public class RouteCostCalculatorTests
{
    // Fixed UTC hours that fall within day (10:00) and night (23:00) windows
    private static readonly DateTime Day   = new(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Night = new(2025, 6, 1, 23, 0, 0, DateTimeKind.Utc);

    private static RouteCostCalculator Create(
        decimal baseFare      = 2000m,
        decimal perMeter      = 0.5m,
        decimal dayFactor     = 1.0m,
        decimal nightFactor   = 1.3m,
        int     dayStartHour  = 6,
        int     nightStartHour = 22)
        => new(Options.Create(new RouteCostOptions
        {
            BaseFare       = baseFare,
            PerMeter       = perMeter,
            DayFactor      = dayFactor,
            NightFactor    = nightFactor,
            DayStartHour   = dayStartHour,
            NightStartHour = nightStartHour,
        }));

    [Test]
    public async Task Calculate_Returns_BaseFare_For_Zero_Distance_During_Day()
    {
        var cost = Create().Calculate(distanceMeters: 0, at: Day);

        await Assert.That(cost).IsEqualTo(2000m); // 2000 × 1.0
    }

    [Test]
    public async Task Calculate_Adds_PerMeter_Component()
    {
        var calc = Create(baseFare: 0m, perMeter: 0.5m, dayFactor: 1.0m);

        // 1 000 m × 0.5 = 500
        var cost = calc.Calculate(distanceMeters: 1_000, at: Day);

        await Assert.That(cost).IsEqualTo(500m);
    }

    [Test]
    public async Task Calculate_Applies_DayFactor()
    {
        var calc = Create(baseFare: 1000m, perMeter: 0m, dayFactor: 1.0m);

        var cost = calc.Calculate(distanceMeters: 0, at: Day);

        await Assert.That(cost).IsEqualTo(1000m);
    }

    [Test]
    public async Task Calculate_Applies_NightFactor()
    {
        var calc = Create(baseFare: 1000m, perMeter: 0m, nightFactor: 1.3m);

        var cost = calc.Calculate(distanceMeters: 0, at: Night);

        await Assert.That(cost).IsEqualTo(1300m);
    }

    [Test]
    public async Task Calculate_Combines_BaseFare_PerMeter_And_DayFactor()
    {
        var calc = Create(baseFare: 2000m, perMeter: 0.5m, dayFactor: 1.0m);

        // (2000 + 0.5 × 1000) × 1.0 = 2500
        var cost = calc.Calculate(distanceMeters: 1_000, at: Day);

        await Assert.That(cost).IsEqualTo(2500m);
    }

    [Test]
    public async Task Calculate_Applies_NightSurcharge()
    {
        var calc = Create(baseFare: 2000m, perMeter: 0.5m, nightFactor: 1.3m);

        // (2000 + 0.5 × 1000) × 1.3 = 3250
        var cost = calc.Calculate(distanceMeters: 1_000, at: Night);

        await Assert.That(cost).IsEqualTo(3250m);
    }

    [Test]
    public async Task Calculate_Clamps_To_Zero_When_Result_Is_Negative()
    {
        var calc = Create(baseFare: -5000m, perMeter: 0m);

        var cost = calc.Calculate(distanceMeters: 0, at: Day);

        await Assert.That(cost).IsEqualTo(0m);
    }

    [Test]
    public async Task Calculate_Rounds_To_Two_Decimal_Places()
    {
        // 1 m × (1/3) ≈ 0.3333... → rounds to 0.33
        var calc = Create(baseFare: 0m, perMeter: 1m / 3m, dayFactor: 1.0m);

        var cost = calc.Calculate(distanceMeters: 1, at: Day);

        await Assert.That(cost).IsEqualTo(0.33m);
    }

    [Test]
    public async Task Calculate_Uses_Default_Options_Values()
    {
        var calc = new RouteCostCalculator(Options.Create(new RouteCostOptions()));

        // defaults: BaseFare=2000, PerMeter=0.5, DayFactor=1.0; 0 m at daytime → 2000
        var cost = calc.Calculate(distanceMeters: 0, at: Day);

        await Assert.That(cost).IsEqualTo(2000m);
    }

    [Test]
    public async Task Calculate_Handles_Long_Route_During_Day()
    {
        var calc = Create(baseFare: 2000m, perMeter: 0.5m, dayFactor: 1.0m);

        // (2000 + 0.5 × 50000) × 1.0 = 27000
        var cost = calc.Calculate(distanceMeters: 50_000, at: Day);

        await Assert.That(cost).IsEqualTo(27000m);
    }
}
