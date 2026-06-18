using GridTrack.Application.Dtos;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.Interfaces;

public interface IDispatchStrategy
{
    Task<IReadOnlyList<DispatchCandidateDto>> GetCandidatesAsync(
        Point deliveryLocation, int count, CancellationToken ct);
}
