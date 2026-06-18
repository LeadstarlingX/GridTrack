namespace GridTrack.Application.Dispatch;

// Delivery pricing model: cost = BaseFare + (PerKm × km) + (PerMinute × minutes).
// Defaults are in Syrian Pounds (SYP) to match the seeded Damascus fleet.
public sealed class RouteCostOptions
{
    public const string SectionName = "RouteCost";

    public string Currency  { get; init; } = "SYP";
    public decimal BaseFare  { get; init; } = 2000m;
    public decimal PerKm     { get; init; } = 500m;
    public decimal PerMinute { get; init; } = 50m;
}
