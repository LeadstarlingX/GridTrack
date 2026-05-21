namespace GridTrack.Application.Dtos;

public sealed record DeliveryListItemResponse(
    string Id,
    string Status,
    string DistrictId,
    string? AssignedDriverId,
    string? AssignedDriverName,
    int? EtaSeconds,
    DateTime CreatedAt);

public sealed record GetDeliveriesResponse(
    IReadOnlyList<DeliveryListItemResponse> Items,
    string? NextCursor,
    int? TotalCount);
