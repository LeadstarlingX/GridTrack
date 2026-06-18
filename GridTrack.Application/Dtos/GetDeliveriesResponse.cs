namespace GridTrack.Application.Dtos;

public sealed record DeliveryListItemResponse(
    Guid Id,
    string Status,
    string DistrictId,
    Guid? AssignedDriverId,
    string? AssignedDriverName,
    int? EtaSeconds,
    DateTime CreatedAt,
    decimal? RouteCost);

public sealed record GetDeliveriesResponse(
    IReadOnlyList<DeliveryListItemResponse> Items,
    string? NextCursor,
    int? TotalCount);
