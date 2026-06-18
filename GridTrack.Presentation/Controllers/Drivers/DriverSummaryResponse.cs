using GridTrack.Application.Dtos;

namespace GridTrack.Presentation.Controllers.Drivers;

public sealed record DriverSummaryResponse(
    Guid DriverId,
    string Name,
    string ShortName,
    double Lat,
    double Lng,
    string DistrictId,
    bool IsActive)
{
    public static DriverSummaryResponse From(DriverDto dto) => new(
        dto.DriverId,
        dto.Name,
        dto.ShortName,
        dto.Location.Coordinate.Y,
        dto.Location.Coordinate.X,
        dto.DistrictId,
        dto.IsActive);
}
