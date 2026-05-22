using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Alerts;

public sealed record GetAlertsQuery(
    string? Cursor,
    DateTime? From,
    DateTime? To,
    string? DistrictId,
    string? AnomalyType,
    int PageSize);

public sealed class GetAlertsHandler
{
    public Task<GetAlertsResponse> Handle(
        GetAlertsQuery query,
        IAnomalyReadService readService,
        CancellationToken ct)
        => readService.GetPaginatedAlertsAsync(
            query.Cursor,
            query.From,
            query.To,
            query.DistrictId,
            query.AnomalyType,
            query.PageSize,
            ct);
}
