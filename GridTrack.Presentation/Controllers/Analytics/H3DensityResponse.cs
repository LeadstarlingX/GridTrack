namespace GridTrack.Presentation.Controllers.Analytics;

public sealed record H3DensityResponse(
    IReadOnlyList<H3DensityCell> Cells);