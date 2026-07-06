namespace GridTrack.Application.Dispatch;

// Delivery pricing model: cost = (BaseFare + PerMeter × distanceMeters) × DayNightFactor.
// Defaults are in Syrian Pounds (SYP) to match the seeded Damascus fleet.
public sealed class RouteCostOptions
{
    public const string SectionName = "RouteCost";

    public string  Currency       { get; init; } = "SYP";
    public decimal BaseFare       { get; init; } = 2000m;
    public decimal PerMeter       { get; init; } = 0.5m;
    public decimal DayFactor      { get; init; } = 1.0m;
    public decimal NightFactor    { get; init; } = 1.3m;
    public int     DayStartHour   { get; init; } = 6;   // inclusive
    public int     NightStartHour { get; init; } = 22;  // inclusive
}
