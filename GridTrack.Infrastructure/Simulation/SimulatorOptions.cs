namespace GridTrack.Infrastructure.Simulation;

public sealed class SimulatorOptions
{
    public bool Enabled { get; set; } = true;
    public int PositionUpdateIntervalMs { get; set; } = 500;
    public int StallThresholdSeconds { get; set; } = 60;
    public int StallPauseProbabilityPct { get; set; } = 2;
    public int StallPauseDurationSeconds { get; set; } = 90;
    // Delivery lifecycle
    public int DeliveryAssignProbabilityPct { get; set; } = 12;
    public int CancellationProbabilityPct { get; set; } = 3;
    public double EtaBufferMultiplier { get; set; } = 1.1;
    public int DeliveryReloadIntervalTicks { get; set; } = 60;
}
