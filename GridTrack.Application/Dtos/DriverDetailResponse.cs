namespace GridTrack.Application.Dtos;

public sealed record DriverDetailResponse(
    Guid Id,
    string Name,
    string ShortName,
    string DistrictId,
    bool IsActive,
    string? CarType,
    string? LicensePlate,
    string? PhoneNumber,
    double Lat,
    double Lng,
    DateTime LastSeen);
