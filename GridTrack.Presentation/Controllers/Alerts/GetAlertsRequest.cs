namespace GridTrack.Presentation.Controllers.Alerts;

public sealed record GetAlertsRequest(
    string? Cursor,
    DateTime? From,
    DateTime? To,
    string? DistrictId,
    string? AnomalyType,
    int? PageSize
);