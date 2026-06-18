using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record GetDriversQuery(
    string? Cursor,
    string? DistrictId,
    string? Status,
    string? Search,
    int PageSize);

public sealed class GetDriversHandler
{
    public Task<GetDriversResponse> Handle(
        GetDriversQuery query,
        IDriverReadService readService,
        CancellationToken ct)
        => readService.GetAllAsync(query.Cursor, query.DistrictId, query.Status, query.Search, query.PageSize, ct);
}
