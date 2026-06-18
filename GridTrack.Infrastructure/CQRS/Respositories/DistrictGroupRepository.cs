using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.DistrictGroups;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Infrastructure.CQRS.Respositories;

internal sealed class DistrictGroupRepository(AppDbContext context) : IDistrictGroupRepository
{
    public async Task AddAsync(DistrictGroup group, CancellationToken ct)
        => await context.Set<DistrictGroup>().AddAsync(group, ct);

    public Task<DistrictGroup?> GetByIdAsync(Guid id, CancellationToken ct)
        => context.Set<DistrictGroup>().FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<DistrictGroup>> GetAllAsync(CancellationToken ct)
        => await context.Set<DistrictGroup>().OrderBy(g => g.Name).ToListAsync(ct);

    public Task RemoveAsync(DistrictGroup group, CancellationToken ct)
    {
        context.Set<DistrictGroup>().Remove(group);
        return Task.CompletedTask;
    }
}
