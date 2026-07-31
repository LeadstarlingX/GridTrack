using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Authentication;

public sealed record GetUsersQuery;

public sealed class GetUsersHandler
{
    public async Task<IReadOnlyList<UserDto>> Handle(
        GetUsersQuery query,
        IUserRepository users,
        CancellationToken ct)
    {
        var all = await users.GetAllAsync(ct);
        return all.Select(u => new UserDto(u.UserId, u.Username, u.Role, u.SectorIds)).ToArray();
    }
}
