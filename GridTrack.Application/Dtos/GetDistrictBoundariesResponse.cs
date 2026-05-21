namespace GridTrack.Application.Dtos;

public sealed record GeoJsonMultiPolygonGeometryResponse(
    string Type,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<double[]>>> Coordinates);

public sealed record GeoJsonDistrictPropertiesResponse(string DistrictId, string Name);

public sealed record GeoJsonDistrictFeatureResponse(
    string Type,
    GeoJsonDistrictPropertiesResponse Properties,
    GeoJsonMultiPolygonGeometryResponse Geometry);

public sealed record GetDistrictBoundariesResponse(
    string Type,
    IReadOnlyList<GeoJsonDistrictFeatureResponse> Features);
