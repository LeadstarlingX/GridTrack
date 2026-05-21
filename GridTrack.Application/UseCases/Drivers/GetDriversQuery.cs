using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record GetDriversQuery(
    string? Cursor,
    string? DistrictId,
    string? Status,
    int PageSize);

public sealed class GetDriversHandler
{
    public Task<GetDriversResponse> Handle(GetDriversQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}
