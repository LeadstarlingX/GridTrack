using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Infrastructure.CQRS.Respositories;

public sealed class DeliveryRepository : IDeliveryRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public DeliveryRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Delivery?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Set<Delivery>()
            .FirstOrDefaultAsync(d => d.DeliveryId == id, ct);
    }

    public async Task<IEnumerable<Delivery>> GetActiveByDistrictAsync(string districtId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Set<Delivery>()
            .Where(d => d.DistrictId == districtId &&
                        d.Status != DeliveryStatus.Delivered &&
                        d.Status != DeliveryStatus.Cancelled)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Delivery delivery, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await context.Set<Delivery>().AddAsync(delivery, ct);
    }

    public async Task UpdateAsync(Delivery delivery, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Set<Delivery>().Update(delivery);
    }
}