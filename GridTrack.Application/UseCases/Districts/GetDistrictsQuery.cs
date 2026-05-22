using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Districts;

public sealed record GetDistrictsQuery();

public sealed class GetDistrictsHandler
{
    public Task<GetDistrictsResponse> Handle(
        GetDistrictsQuery query,
        IDistrictReadService readService,
        CancellationToken ct)
        => readService.GetDistrictsAsync(ct);
}
