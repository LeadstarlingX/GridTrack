namespace GridTrack.Infrastructure.Simulation;

public sealed class SimulatorOptions
{
    public bool Enabled { get; set; } = true;
    public int PositionUpdateIntervalMs { get; set; } = 1000;
    public int StallThresholdSeconds { get; set; } = 60;
    public int StallPauseProbabilityPct { get; set; } = 2;
    public int StallPauseDurationSeconds { get; set; } = 90;
}
