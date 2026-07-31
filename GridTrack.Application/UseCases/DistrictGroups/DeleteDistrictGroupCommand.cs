using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.DistrictGroups;

namespace GridTrack.Application.UseCases.DistrictGroups;

public sealed record DeleteDistrictGroupCommand(Guid Id);

public sealed class DeleteDistrictGroupHandler
{
    public async Task<Result> Handle(
        DeleteDistrictGroupCommand command,
        IDistrictGroupRepository repository,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var group = await repository.GetByIdAsync(command.Id, ct);
        if (group is null)
            return Result.Failure(DistrictGroupErrors.NotFound);

        // Remove the deleted group from any observer's SectorIds so they don't
        // retain a phantom UUID that silently resolves to zero districts at next login.
        var allUsers = await users.GetAllAsync(ct);
        foreach (var user in allUsers.Where(u => u.SectorIds.Contains(command.Id)))
            user.UpdateSectors(user.SectorIds.Where(s => s != command.Id).ToArray());

        await repository.RemoveAsync(group, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
