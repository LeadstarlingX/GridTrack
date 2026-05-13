namespace GridTrack.Application.Dtos;

public sealed record DeliveryCreatedResponse(
    Guid DeliveryId,
    string DistrictId,
    DateTime CreatedAt);
