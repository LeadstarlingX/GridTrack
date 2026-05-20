using GridTrack.Presentation.Controllers.Shared;

namespace GridTrack.Presentation.Controllers.Districts;

public class GeoJsonGeometry
{
    public string Type { get; set; } = null!;
    public List<List<Coordinate>> Coordinates { get; set; } = null!;
}