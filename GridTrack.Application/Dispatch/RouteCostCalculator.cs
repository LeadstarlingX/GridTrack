using Microsoft.Extensions.Options;

namespace GridTrack.Application.Dispatch;

public interface IRouteCostCalculator
{
    // Computes delivery cost from distance and the time the route was calculated.
    // Result is in the configured currency (SYP by default), rounded to 2 decimal places.
    decimal Calculate(double distanceMeters, DateTime at);
}

public sealed class RouteCostCalculator(IOptions<RouteCostOptions> options) : IRouteCostCalculator
{
    private readonly RouteCostOptions _opts = options.Value;

    public decimal Calculate(double distanceMeters, DateTime at)
    {
        var isDay  = at.Hour >= _opts.DayStartHour && at.Hour < _opts.NightStartHour;
        var factor = isDay ? _opts.DayFactor : _opts.NightFactor;
        var cost   = (_opts.BaseFare + _opts.PerMeter * (decimal)distanceMeters) * factor;
        return Math.Round(Math.Max(cost, 0m), 2);
    }
}
