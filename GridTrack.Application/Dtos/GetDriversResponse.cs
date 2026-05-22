namespace GridTrack.Application.Dtos;

public sealed record DriverListItemResponse(
    Guid DriverId,
    string Name,
    string ShortName,
    double Lat,
    double Lng,
    string DistrictId,
    string Status,
    long ActiveDeliveries,
    long CompletedToday,
    bool HasAnomaly,
    string? AnomalyReason);

public sealed record GetDriversResponse(
    IReadOnlyList<DriverListItemResponse> Items,
    string? NextCursor,
    int? TotalCount);
