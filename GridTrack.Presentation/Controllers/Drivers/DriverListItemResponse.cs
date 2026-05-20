using GridTrack.Presentation.Controllers.Shared;

namespace GridTrack.Presentation.Controllers.Drivers;

public class DriverListItemResponse
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string DistrictId { get; set; } = null!;
    public string DistrictName { get; set; } = null!;
    public Coordinate Position { get; set; } = null!;
}