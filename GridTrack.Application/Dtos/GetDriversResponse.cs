namespace GridTrack.Application.Dtos;

public sealed record DriverListItemResponse(
    string Id,
    string Name,
    string Status,
    string DistrictId,
    string? DistrictName,
    double Lat,
    double Lng);

public sealed record GetDriversResponse(
    IReadOnlyList<DriverListItemResponse> Items,
    string? NextCursor,
    int? TotalCount);
