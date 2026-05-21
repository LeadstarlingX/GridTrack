using GridTrack.Application.Dtos;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IDriverReadService
{
    Task<IEnumerable<DriverDto>> GetByDistrictAsync(string districtId, CancellationToken ct);
    Task<IEnumerable<DriverDto>> GetNearestAsync(Point location, int count, CancellationToken ct);
}
