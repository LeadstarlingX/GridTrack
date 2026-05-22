namespace GridTrack.Infrastructure.Data;

public sealed class DeliveryRoute
{
    public Guid DeliveryId { get; set; }
    public int Sequence { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}
