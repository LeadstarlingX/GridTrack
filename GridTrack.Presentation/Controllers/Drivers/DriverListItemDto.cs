namespace GridTrack.Presentation.Controllers.Drivers;

public class DriverListItemDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string DistrictId { get; set; } = null!;
    public string DistrictName { get; set; } = null!;
    public double Lat { get; set; }
    public double Lng { get; set; }
}