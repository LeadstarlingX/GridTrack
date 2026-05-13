using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.Interfaces;

public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<Driver>> GetActiveByDistrictAsync(string districtId, CancellationToken ct);
    Task<IEnumerable<Driver>> GetNearestAsync(Point location, int count, CancellationToken ct);
    Task UpdateAsync(Driver driver, CancellationToken ct);
}
