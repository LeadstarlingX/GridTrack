using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Deliveries;
using GridTrack.Infrastructure.DbContext;

namespace GridTrack.Infrastructure.CQRS.Respositories;

public sealed class DeliveryRepository : IDeliveryRepository
{
    private readonly AppDbContext _context;

    public DeliveryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Delivery delivery, CancellationToken ct)
        => await _context.Set<Delivery>().AddAsync(delivery, ct);

    public Task UpdateAsync(Delivery delivery, CancellationToken ct)
    {
        _context.Set<Delivery>().Update(delivery);
        return Task.CompletedTask;
    }
}
