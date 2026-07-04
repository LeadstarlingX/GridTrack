using GridTrack.Application.Dispatch;
using Microsoft.Extensions.Options;

namespace GridTrack.Application.UnitTests.Dispatch;

public class RouteCostCalculatorTests
{
    private static RouteCostCalculator Create(decimal baseFare, decimal perKm, decimal perMinute)
        => new(Options.Create(new RouteCostOptions
        {
            BaseFare  = baseFare,
            PerKm     = perKm,
            PerMinute = perMinute,
        }));

    [Test]
    public async Task Calculate_Returns_BaseFare_For_Zero_Distance_And_Duration()
    {
        var calc = Create(baseFare: 2000m, perKm: 500m, perMinute: 50m);

        var cost = calc.Calculate(distanceMeters: 0, durationSeconds: 0);

        await Assert.That(cost).IsEqualTo(2000m);
    }

    [Test]
    public async Task Calculate_Adds_PerKm_Component()
    {
        var calc = Create(baseFare: 0m, perKm: 500m, perMinute: 0m);

        // 5 000 m = 5 km → 5 × 500 = 2 500
        var cost = calc.Calculate(distanceMeters: 5_000, durationSeconds: 0);

        await Assert.That(cost).IsEqualTo(2500m);
    }

    [Test]
    public async Task Calculate_Adds_PerMinute_Component()
    {
        var calc = Create(baseFare: 0m, perKm: 0m, perMinute: 50m);

        // 120 s = 2 min → 2 × 50 = 100
        var cost = calc.Calculate(distanceMeters: 0, durationSeconds: 120);

        await Assert.That(cost).IsEqualTo(100m);
    }

    [Test]
    public async Task Calculate_Combines_All_Components()
    {
        var calc = Create(baseFare: 2000m, perKm: 500m, perMinute: 50m);

        // 3 km, 10 min → 2000 + 1500 + 500 = 4000
        var cost = calc.Calculate(distanceMeters: 3_000, durationSeconds: 600);

        await Assert.That(cost).IsEqualTo(4000m);
    }

    [Test]
    public async Task Calculate_Clamps_To_Zero_When_Result_Is_Negative()
    {
        // Negative base fare, no distance/time → would be negative without clamping
        var calc = Create(baseFare: -5000m, perKm: 0m, perMinute: 0m);

        var cost = calc.Calculate(distanceMeters: 0, durationSeconds: 0);

        await Assert.That(cost).IsEqualTo(0m);
    }

    [Test]
    public async Task Calculate_Rounds_To_Two_Decimal_Places()
    {
        var calc = Create(baseFare: 0m, perKm: 1m, perMinute: 0m);

        // 1 m = 0.001 km → raw cost = 0.001 → Math.Round(..., 2) = 0.00
        var cost = calc.Calculate(distanceMeters: 1, durationSeconds: 0);

        await Assert.That(cost).IsEqualTo(0.00m);
    }

    [Test]
    public async Task Calculate_Uses_Default_Options_Values()
    {
        var calc = new RouteCostCalculator(Options.Create(new RouteCostOptions()));

        // defaults: BaseFare=2000, PerKm=500, PerMinute=50; 0 km, 0 min → 2000
        var cost = calc.Calculate(distanceMeters: 0, durationSeconds: 0);

        await Assert.That(cost).IsEqualTo(2000m);
    }

    [Test]
    public async Task Calculate_Handles_Long_Route()
    {
        var calc = Create(baseFare: 2000m, perKm: 500m, perMinute: 50m);

        // 50 km, 60 min → 2000 + 25000 + 3000 = 30000
        var cost = calc.Calculate(distanceMeters: 50_000, durationSeconds: 3_600);

        await Assert.That(cost).IsEqualTo(30000m);
    }
}
