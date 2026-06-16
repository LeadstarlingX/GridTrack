namespace GridTrack.Infrastructure.Simulation;

public sealed class SimulatorOptions
{
    public bool Enabled { get; set; } = true;
    public int PositionUpdateIntervalMs { get; set; } = 500;
    public int StallThresholdSeconds { get; set; } = 300;
    public int StallPauseProbabilityPct { get; set; } = 2;
    public int StallPauseDurationSeconds { get; set; } = 360;
    // Delivery lifecycle
    public int DeliveryAssignProbabilityPct { get; set; } = 12;
    public int CancellationProbabilityPct { get; set; } = 3;
    public double EtaBufferMultiplier { get; set; } = 1.1;
    public int DeliveryReloadIntervalTicks { get; set; } = 60;
    // Anomaly / surge / incident simulation
    public int AnomalyIntervalMs { get; set; } = 15_000;
    public int SurgeIntervalMs { get; set; } = 30_000;
    public int IncidentIntervalMs { get; set; } = 60_000;
}
