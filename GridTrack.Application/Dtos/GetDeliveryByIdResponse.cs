namespace GridTrack.Application.Dtos;

public sealed record GetDeliveryByIdResponse(
    Guid Id,
    string Status,
    string DistrictId,
    Guid? AssignedDriverId,
    string? AssignedDriverName,
    int? EtaSeconds,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<CoordinateResponse> RoutePolyline);
