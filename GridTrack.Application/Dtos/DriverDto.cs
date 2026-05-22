using NetTopologySuite.Geometries;

namespace GridTrack.Application.Dtos;

public sealed class DriverDto
{
    public Guid DriverId { get; init; }
    public Geometry Location { get; init; } = null!;
    public bool IsActive { get; init; }
    public DateTime LastSeen { get; init; }
    public string DistrictId { get; init; } = string.Empty;
}