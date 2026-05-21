namespace GridTrack.Application.Dtos;

public sealed record GetDeliveryByIdResponse(
    string Id,
    string Status,
    string DistrictId,
    string? AssignedDriverId,
    string? AssignedDriverName,
    int? EtaSeconds,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<CoordinateResponse> RoutePolyline);
