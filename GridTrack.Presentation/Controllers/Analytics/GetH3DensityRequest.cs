namespace GridTrack.Presentation.Controllers.Analytics;

public sealed record GetH3DensityRequest(
    DateTime From,
    DateTime To,
    int Resolution,
    int? FromHour,
    int? ToHour
    );