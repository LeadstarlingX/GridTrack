using GridTrack.Domain.Drivers;

namespace GridTrack.Application.CQRS.Repositories;

public interface IDriverRepository
{
    Task AddAsync(Driver driver, CancellationToken ct);
    Task UpdateAsync(Driver driver, CancellationToken ct);
}
