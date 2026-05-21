namespace GridTrack.Application.Dtos;

public sealed record DriverAvailabilityResponse(
    string Id,
    string Status,
    DateTime UpdatedAt);
