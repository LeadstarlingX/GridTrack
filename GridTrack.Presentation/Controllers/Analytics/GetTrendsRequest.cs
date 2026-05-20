namespace GridTrack.Presentation.Controllers.Analytics;

public sealed record GetTrendsRequest(
    DateTime From,
    DateTime To,
    string Granularity
    );