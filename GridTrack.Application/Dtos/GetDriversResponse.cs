namespace GridTrack.Application.Dtos;

public sealed record DriverListItemResponse(
    string DriverId,
    string Name,
    string ShortName,
    double Lat,
    double Lng,
    string DistrictId,
    string Status,
    int ActiveDeliveries,
    int CompletedToday,
    bool HasAnomaly,
    string? AnomalyReason);

public sealed record GetDriversResponse(
    IReadOnlyList<DriverListItemResponse> Items,
    string? NextCursor,
    int? TotalCount);
