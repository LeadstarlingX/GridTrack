using GridTrack.Domain.DistrictGroups;

namespace GridTrack.Application.CQRS.Repositories;

public interface IDistrictGroupRepository
{
    Task AddAsync(DistrictGroup group, CancellationToken ct);
    Task<DistrictGroup?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DistrictGroup>> GetAllAsync(CancellationToken ct);
    Task RemoveAsync(DistrictGroup group, CancellationToken ct);
}
