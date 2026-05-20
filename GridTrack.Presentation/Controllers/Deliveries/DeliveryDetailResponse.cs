using GridTrack.Presentation.Controllers.Shared;

namespace GridTrack.Presentation.Controllers.Deliveries;

public class DeliveryDetailResponse
{
    public string Id { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string DistrictId { get; set; } = null!;
    public string? AssignedDriverId { get; set; }
    public string? AssignedDriverName { get; set; }
    public int? EtaSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Coordinate> RoutePolyline { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

