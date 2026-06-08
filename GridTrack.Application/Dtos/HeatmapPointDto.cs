namespace GridTrack.Application.Dtos;

public sealed class HeatmapPointDto
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public double Intensity { get; set; }
}
