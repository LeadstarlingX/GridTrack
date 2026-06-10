using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetDistrictVolumeQuery(DateTime? From, DateTime? To);

public sealed class GetDistrictVolumeHandler
{
    public Task<GetDistrictVolumeResponse> Handle(
        GetDistrictVolumeQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetDistrictVolumeAsync(query.From, query.To, ct);
}
