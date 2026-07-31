namespace GridTrack.Application.Dtos;

public sealed record UserDto(Guid UserId, string Username, string Role, Guid[] SectorIds);
