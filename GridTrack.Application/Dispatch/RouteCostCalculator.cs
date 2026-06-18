using Microsoft.Extensions.Options;

namespace GridTrack.Application.Dispatch;

public interface IRouteCostCalculator
{
    // Computes delivery cost from an OSRM route's distance + duration. Result is in
    // the configured currency (SYP by default), rounded to 2 decimal places.
    decimal Calculate(double distanceMeters, double durationSeconds);
}

public sealed class RouteCostCalculator(IOptions<RouteCostOptions> options) : IRouteCostCalculator
{
    private readonly RouteCostOptions _opts = options.Value;

    public decimal Calculate(double distanceMeters, double durationSeconds)
    {
        var km      = (decimal)(distanceMeters / 1000.0);
        var minutes = (decimal)(durationSeconds / 60.0);

        var cost = _opts.BaseFare
                 + (_opts.PerKm * km)
                 + (_opts.PerMinute * minutes);

        return Math.Round(Math.Max(cost, 0m), 2);
    }
}
