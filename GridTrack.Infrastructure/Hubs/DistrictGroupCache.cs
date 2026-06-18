using GridTrack.Domain.DistrictGroups;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Infrastructure.Hubs;

// Thread-safe in-memory cache that maps districtId → district group IDs.
// Reloads from the database at most once every 5 minutes; call Invalidate()
// after any district_groups write to force an immediate reload.
internal sealed class DistrictGroupCache(IDbContextFactory<AppDbContext> dbFactory) : IDistrictGroupCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile Dictionary<string, Guid[]>? _map;
    private DateTime _loadedAt;

    public async Task<IReadOnlyList<Guid>> GetGroupIdsForDistrictAsync(string districtId, CancellationToken ct)
    {
        var map = await GetOrRefreshAsync(ct);
        return map.TryGetValue(districtId, out var ids) ? ids : [];
    }

    public void Invalidate() => _map = null;

    private async Task<Dictionary<string, Guid[]>> GetOrRefreshAsync(CancellationToken ct)
    {
        var snapshot = _map;
        if (snapshot is not null && DateTime.UtcNow - _loadedAt < Ttl)
            return snapshot;

        await _lock.WaitAsync(ct);
        try
        {
            snapshot = _map;
            if (snapshot is not null && DateTime.UtcNow - _loadedAt < Ttl)
                return snapshot;

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var groups = await db.Set<DistrictGroup>().AsNoTracking().ToListAsync(ct);

            var map = new Dictionary<string, Guid[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in groups)
            {
                foreach (var d in g.DistrictIds)
                {
                    map[d] = map.TryGetValue(d, out var existing)
                        ? [.. existing, g.Id]
                        : [g.Id];
                }
            }

            _map      = map;
            _loadedAt = DateTime.UtcNow;
            return map;
        }
        finally
        {
            _lock.Release();
        }
    }
}
