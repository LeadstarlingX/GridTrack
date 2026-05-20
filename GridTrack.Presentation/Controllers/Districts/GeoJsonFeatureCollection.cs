namespace GridTrack.Presentation.Controllers.Districts;

public class GeoJsonFeatureCollectionResponse
{
    public string Type { get; set; } = "FeatureCollection";
    public List<GeoJsonFeature> Features { get; set; } = null!;
}