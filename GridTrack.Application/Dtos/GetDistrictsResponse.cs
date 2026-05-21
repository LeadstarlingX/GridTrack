namespace GridTrack.Application.Dtos;

public sealed record DistrictItemResponse(
    string Id,
    string Name,
    CoordinateResponse Centroid);

public sealed record GetDistrictsResponse(IReadOnlyList<DistrictItemResponse> Items);
