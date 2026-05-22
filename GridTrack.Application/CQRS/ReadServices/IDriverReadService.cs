using GridTrack.Application.Dtos;
using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IDriverReadService
{
    Task<IEnumerable<DriverDto>> GetByDistrictAsync(string districtId, CancellationToken ct);
    Task<IEnumerable<DriverDto>> GetNearestAsync(Point location, int count, CancellationToken ct);
    Task<Driver?> GetAggregateByIdAsync(Guid id, CancellationToken ct);
    Task<GetDriversResponse> GetAllAsync(string? cursor, string? districtId, string? status, int pageSize, CancellationToken ct);
}
