namespace GridTrack.Presentation.Controllers.Drivers;

public sealed record CreateDriverHttpRequest(
    double Lat,
    double Lng,
    string Name,
    string ShortName,
    string? DistrictId,
    bool IsActive = true);
