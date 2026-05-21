namespace GridTrack.Application.Dtos;

public sealed record H3DensityCellResponse(
    string H3Index,
    double Lat,
    double Lng,
    int DeliveryCount);

public sealed record GetH3DensityResponse(IReadOnlyList<H3DensityCellResponse> Cells);
