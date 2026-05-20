namespace GridTrack.Presentation.Controllers.Districts;

public class GeoJsonGeometry
{
    public string Type { get; set; } = null!;
    public List<List<List<double>>> Coordinates { get; set; } = null!;
}