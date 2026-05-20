namespace GridTrack.Presentation.Controllers.Drivers;

public sealed record GetDriversRequest(
    string? Cursor,
    string? Status,
    string? DistrictId,
    int? PageSize
    );