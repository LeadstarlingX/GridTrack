namespace GridTrack.Presentation.Controllers.Drivers;

public class DriverAvailabilityResponse
{
    public string Id { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}