using GridTrack.Domain.Authentication;

namespace GridTrack.Application.CQRS.Repositories;

public interface IUserRepository
{
    Task AddAsync(AppUser user, CancellationToken ct);
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct);
}