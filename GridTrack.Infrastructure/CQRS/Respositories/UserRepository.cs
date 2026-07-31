using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Authentication;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Infrastructure.CQRS.Respositories;

internal sealed class UserRepository : IUserRepository
{   
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;

    }

    public async Task AddAsync(AppUser user, CancellationToken ct)
        => await _context.Set<AppUser>().AddAsync(user, ct);

    public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct)
        => _context.Set<AppUser>().FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken ct)
        => _context.Set<AppUser>().FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct)
        => await _context.Set<AppUser>().ToListAsync(ct);
}