using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.CQRS.Repositories;

public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<Driver>> GetActiveByDistrictAsync(string districtId, CancellationToken ct);
    Task<IEnumerable<Driver>> GetNearestAsync(Point location, int count, CancellationToken ct);
    Task AddAsync(Driver driver, CancellationToken ct);
    Task UpdateAsync(Driver driver, CancellationToken ct);
}
