using GridTrack.Application.Abstractions.Authentication;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Errors;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Authentication;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginCommand(LoginRequest Request);

public sealed record LoginResponse(string Token, string Role);

public sealed class LoginHandler
{
    public async Task<Result<LoginResponse>> Handle(
        LoginCommand     command,
        IUserRepository  users,
        IDistrictGroupRepository groups,
        IPasswordHasher  hasher,
        ILocalJwtService jwt,
        CancellationToken ct)
    {
        var user = await users.GetByUsernameAsync(command.Request.Username, ct);

        if (user is null || !hasher.Verify(command.Request.Password, user.PasswordHash))
            return Result.Failure<LoginResponse>(AuthErrors.InvalidCredentials);

        IReadOnlyList<string>? districtIds = null;
        Guid[]? sectorIds = null;

        if (!user.IsGeneralObserver)
        {
            sectorIds = user.SectorIds;

            if (sectorIds.Length > 0)
            {
                var allGroups = await groups.GetAllAsync(ct);
                districtIds = allGroups
                    .Where(g => sectorIds.Contains(g.Id))
                    .SelectMany(g => g.DistrictIds)
                    .Distinct()
                    .ToList();
            }
            else
            {
                // Observer with no sectors assigned — explicitly deny all data.
                districtIds = [];
            }
        }

        var token = jwt.Issue(user.UserId, user.Role, districtIds, sectorIds);
        return Result.Success(new LoginResponse(token, user.Role));
    }
}