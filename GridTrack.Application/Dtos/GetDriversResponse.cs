namespace GridTrack.Application.Dtos;

public sealed record DriverListItemResponse(
    Guid Id,
    string Name,
    string ShortName,
    double Lat,
    double Lng,
    string DistrictId,
    string DistrictName,
    string Status,
    long ActiveDeliveries,
    long CompletedToday,
    bool HasAnomaly,
    string? AnomalyReason,
    string? CarType,
    string? LicensePlate,
    string? PhoneNumber);

public sealed record GetDriversResponse(
    IReadOnlyList<DriverListItemResponse> Items,
    string? NextCursor,
    int? TotalCount);
