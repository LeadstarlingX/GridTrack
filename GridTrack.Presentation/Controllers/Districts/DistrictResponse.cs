using GridTrack.Presentation.Controllers.Shared;

namespace GridTrack.Presentation.Controllers.Districts;

public class DistrictResponse
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Coordinate Centroid { get; set; } = null!;
}