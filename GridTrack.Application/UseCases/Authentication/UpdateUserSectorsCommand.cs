using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Authentication;

namespace GridTrack.Application.UseCases.Authentication;

public sealed record UpdateUserSectorsRequest(Guid[] SectorIds);
public sealed record UpdateUserSectorsCommand(Guid UserId, UpdateUserSectorsRequest Request);

public sealed class UpdateUserSectorsHandler
{
    public async Task<Result<UserDto>> Handle(
        UpdateUserSectorsCommand command,
        IUserRepository users,
        IDistrictGroupRepository groups,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return Result.Failure<UserDto>(AppUserErrors.NotFound);

        if (user.IsGeneralObserver)
            return Result.Failure<UserDto>(AppUserErrors.CannotAssignSectors);

        if (command.Request.SectorIds.Length > 0)
        {
            var allGroups = await groups.GetAllAsync(ct);
            var allGroupIds = allGroups.Select(g => g.Id).ToHashSet();
            var unknown = command.Request.SectorIds.Where(id => !allGroupIds.Contains(id)).ToArray();
            if (unknown.Length > 0)
                return Result.Failure<UserDto>(new Error(
                    "AppUser.UnknownSectorIds",
                    $"Unknown sector IDs: {string.Join(", ", unknown)}"));
        }

        user.UpdateSectors(command.Request.SectorIds);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new UserDto(user.UserId, user.Username, user.Role, user.SectorIds));
    }
}
