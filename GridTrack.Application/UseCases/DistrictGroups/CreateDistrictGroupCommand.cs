using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.DistrictGroups;

namespace GridTrack.Application.UseCases.DistrictGroups;

public sealed record CreateDistrictGroupRequest(string Name, string[] DistrictIds);
public sealed record CreateDistrictGroupCommand(CreateDistrictGroupRequest Request);

public sealed class CreateDistrictGroupHandler
{
    public async Task<Result<DistrictGroupDto>> Handle(
        CreateDistrictGroupCommand command,
        IDistrictGroupRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var result = DistrictGroup.Create(Guid.NewGuid(), command.Request.Name, command.Request.DistrictIds);
        if (result.IsFailure)
            return Result.Failure<DistrictGroupDto>(result.Error);

        await repository.AddAsync(result.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new DistrictGroupDto(result.Value.Id, result.Value.Name, result.Value.DistrictIds));
    }
}
