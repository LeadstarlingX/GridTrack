using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UseCases.Dispatch;

public sealed record GetDispatchCandidatesQuery(Point Location, int Count = 10);

public sealed class GetDispatchCandidatesHandler
{
    public Task<IReadOnlyList<DispatchCandidateDto>> Handle(
        GetDispatchCandidatesQuery query, IDispatchStrategy strategy, CancellationToken ct)
        => strategy.GetCandidatesAsync(query.Location, query.Count, ct);
}
