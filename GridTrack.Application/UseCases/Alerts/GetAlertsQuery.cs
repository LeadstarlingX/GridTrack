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
    public Task<GetAlertsResponse> Handle(GetAlertsQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}
