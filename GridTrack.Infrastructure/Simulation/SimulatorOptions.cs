namespace GridTrack.Infrastructure.Simulation;

public sealed class SimulatorOptions
{
    public bool Enabled { get; set; } = true;
    public int PositionUpdateIntervalMs { get; set; } = 500;
    // Must be shorter than StallPauseDurationSeconds so StallDetected fires while the pause is active
    public int StallThresholdSeconds { get; set; } = 20;
    public int StallPauseProbabilityPct { get; set; } = 30;
    public int StallPauseDurationSeconds { get; set; } = 90;
    // Delivery lifecycle
    public int DeliveryAssignProbabilityPct { get; set; } = 25;
    public int CancellationProbabilityPct { get; set; } = 3;
    public int PrePickupCancellationProbabilityPct { get; set; } = 5;
    public double EtaBufferMultiplier { get; set; } = 1.1;
    public int DeliveryReloadIntervalTicks { get; set; } = 60;
    public int DwellMinSeconds { get; set; } = 15;
    public int DwellMaxSeconds { get; set; } = 45;
    // Route deviation: anomaly event fires but the dot stays on the real route
    public int RouteDeviationProbabilityPct { get; set; } = 12;
    public int RouteDeviationDurationTicks { get; set; } = 8;
    public double RouteDeviationRadiusDeg { get; set; } = 0.003;
    // Anomaly / surge / incident simulation
    public int AnomalyIntervalMs { get; set; } = 5_000;
    public int SurgeIntervalMs { get; set; } = 8_000;
    public int IncidentIntervalMs { get; set; } = 15_000;
}
