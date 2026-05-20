namespace GridTrack.Presentation.Controllers.Deliveries;

public class DeliveryListItemResponse
{
    public string Id { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string DistrictId { get; set; } = null!;
    public string? AssignedDriverId { get; set; }
    public string? AssignedDriverName { get; set; }
    public int? EtaSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
}