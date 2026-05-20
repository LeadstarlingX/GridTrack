namespace GridTrack.Presentation.Controllers.Districts;

public class DistrictDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public CentroidCoordinates Centroid { get; set; } = null!;
}