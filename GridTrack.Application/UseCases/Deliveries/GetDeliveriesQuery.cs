using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record GetDeliveriesQuery(
    string? Cursor,
    string? Status,
    string? DistrictId,
    DateTime? From,
    DateTime? To,
    int PageSize);

public sealed class GetDeliveriesHandler
{
    public Task<GetDeliveriesResponse> Handle(GetDeliveriesQuery query, IDriverReadService _,CancellationToken ct)
        => Task.FromResult(new GetDeliveriesResponse([], null, null));
}
