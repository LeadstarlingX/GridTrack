namespace GridTrack.Application.Dispatch;

public sealed class DispatchWeightsOptions
{
    public const string SectionName = "DispatchWeights";

    public double Proximity              { get; init; } = 0.4;
    public double OnTimeRate             { get; init; } = 0.3;
    public double LoadScore              { get; init; } = 0.2;
    public double ShiftScore             { get; init; } = 0.1;
    // Minimum score gap between 1st and 2nd candidate to trigger direct auto-assignment.
    public double AutoAssignGapThreshold { get; init; } = 0.15;
}
