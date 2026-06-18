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
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var group = await repository.GetByIdAsync(command.Id, ct);
        if (group is null)
            return Result.Failure(DistrictGroupErrors.NotFound);

        await repository.RemoveAsync(group, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
