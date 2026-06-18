using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.DistrictGroups;

namespace GridTrack.Application.UseCases.DistrictGroups;

public sealed record UpdateDistrictGroupRequest(string Name, string[] DistrictIds);
public sealed record UpdateDistrictGroupCommand(Guid Id, UpdateDistrictGroupRequest Request);

public sealed class UpdateDistrictGroupHandler
{
    public async Task<Result> Handle(
        UpdateDistrictGroupCommand command,
        IDistrictGroupRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var group = await repository.GetByIdAsync(command.Id, ct);
        if (group is null)
            return Result.Failure(DistrictGroupErrors.NotFound);

        var result = group.Update(command.Request.Name, command.Request.DistrictIds);
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
