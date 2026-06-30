namespace GridTrack.Application.Dtos;

public sealed record PickupPointResponse(double Lat, double Lng);

public sealed record GetPickupDensityResponse(IReadOnlyList<PickupPointResponse> Points);
