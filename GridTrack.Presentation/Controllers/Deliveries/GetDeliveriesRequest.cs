namespace GridTrack.Presentation.Controllers.Deliveries;

public sealed record GetDeliveriesRequest(
    string? Cursor,
    string? Status,
    string? DistrictId,
    DateTime? From,
    DateTime? To,
    int? PageSize
    );