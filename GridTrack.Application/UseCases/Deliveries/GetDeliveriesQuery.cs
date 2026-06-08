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
    public Task<GetDeliveriesResponse> Handle(
        GetDeliveriesQuery query,
        IDeliveryReadService readService,
        CancellationToken ct)
        => readService.GetAllPaginatedAsync(
            query.Cursor, query.Status, query.DistrictId,
            query.From, query.To, query.PageSize, ct);
}
