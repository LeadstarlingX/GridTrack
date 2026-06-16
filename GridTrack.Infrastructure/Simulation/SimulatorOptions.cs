namespace GridTrack.Infrastructure.Simulation;

public sealed class SimulatorOptions
{
    public bool Enabled { get; set; } = true;
    public int PositionUpdateIntervalMs { get; set; } = 500;
    public int StallThresholdSeconds { get; set; } = 300;
    // Rolled once per delivery at route-start; 8% of transit deliveries will stall
    public int StallPauseProbabilityPct { get; set; } = 8;
    public int StallPauseDurationSeconds { get; set; } = 90;
    // Delivery lifecycle
    public int DeliveryAssignProbabilityPct { get; set; } = 8;
    // Rolled once at pickup arrival; 3% cancel during transit
    public int CancellationProbabilityPct { get; set; } = 3;
    // Rolled once at delivery assignment; 5% cancel before reaching pickup
    public int PrePickupCancellationProbabilityPct { get; set; } = 5;
    public double EtaBufferMultiplier { get; set; } = 1.1;
    public int DeliveryReloadIntervalTicks { get; set; } = 60;
    // Post-delivery rest before next assignment
    public int DwellMinSeconds { get; set; } = 20;
    public int DwellMaxSeconds { get; set; } = 60;
    // Anomaly / surge / incident simulation
    public int AnomalyIntervalMs { get; set; } = 15_000;
    public int SurgeIntervalMs { get; set; } = 30_000;
    public int IncidentIntervalMs { get; set; } = 60_000;
}
