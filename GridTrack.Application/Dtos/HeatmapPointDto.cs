namespace GridTrack.Application.Dtos;

public sealed record HeatmapPointDto(
    decimal Latitude,
    decimal Longitude,
    double Intensity);
