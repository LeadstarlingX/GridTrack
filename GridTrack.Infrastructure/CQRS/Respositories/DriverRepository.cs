
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Drivers;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.CQRS.Respositories;

public sealed class DriverRepository : IDriverRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public DriverRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Set<Driver>()
            .FirstOrDefaultAsync(d => d.DriverId == id, ct);
    }

    public async Task<IEnumerable<Driver>> GetActiveByDistrictAsync(string districtId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Set<Driver>()
            .Where(d => d.DistrictId == districtId && d.IsActive)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Driver>> GetNearestAsync(Point location, int count, CancellationToken ct)
    {   
        
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Set<Driver>()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Location.Distance(location))
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Driver driver, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await context.Set<Driver>().AddAsync(driver, ct);
    }

    public async Task UpdateAsync(Driver driver, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Set<Driver>().Update(driver);
    }
}