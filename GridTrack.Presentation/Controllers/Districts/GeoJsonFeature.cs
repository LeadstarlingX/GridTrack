namespace GridTrack.Presentation.Controllers.Districts;

public class GeoJsonFeature
{
    public string Type { get; set; } = "Feature";
    public DistrictBoundaryProperties Properties { get; set; } = null!;
    public GeoJsonGeometry Geometry { get; set; } = null!;
}