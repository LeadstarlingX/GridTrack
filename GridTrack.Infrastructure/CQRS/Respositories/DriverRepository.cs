using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Drivers;
using GridTrack.Infrastructure.DbContext;

namespace GridTrack.Infrastructure.CQRS.Respositories;

public sealed class DriverRepository : IDriverRepository
{
    private readonly AppDbContext _context;

    public DriverRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Driver driver, CancellationToken ct)
        => await _context.Set<Driver>().AddAsync(driver, ct);

    public Task UpdateAsync(Driver driver, CancellationToken ct)
    {
        _context.Set<Driver>().Update(driver);
        return Task.CompletedTask;
    }
}
