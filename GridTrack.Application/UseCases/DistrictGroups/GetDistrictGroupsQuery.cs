using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.DistrictGroups;

namespace GridTrack.Application.UseCases.DistrictGroups;

public sealed record GetDistrictGroupsQuery;
public sealed record GetDistrictGroupByIdQuery(Guid Id);

public sealed class GetDistrictGroupsHandler
{
    public async Task<IReadOnlyList<DistrictGroupDto>> Handle(
        GetDistrictGroupsQuery query,
        IDistrictGroupRepository repository,
        CancellationToken ct)
    {
        var groups = await repository.GetAllAsync(ct);
        return groups.Select(g => new DistrictGroupDto(g.Id, g.Name, g.DistrictIds)).ToArray();
    }
}

public sealed class GetDistrictGroupByIdHandler
{
    public async Task<Result<DistrictGroupDto>> Handle(
        GetDistrictGroupByIdQuery query,
        IDistrictGroupRepository repository,
        CancellationToken ct)
    {
        var group = await repository.GetByIdAsync(query.Id, ct);
        if (group is null)
            return Result.Failure<DistrictGroupDto>(DistrictGroupErrors.NotFound);
        return Result.Success(new DistrictGroupDto(group.Id, group.Name, group.DistrictIds));
    }
}
