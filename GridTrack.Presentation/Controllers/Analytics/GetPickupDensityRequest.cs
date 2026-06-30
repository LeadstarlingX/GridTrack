namespace GridTrack.Presentation.Controllers.Analytics;

public sealed record GetPickupDensityRequest(
    DateTime From,
    DateTime To,
    int? FromHour,
    int? ToHour
    );
