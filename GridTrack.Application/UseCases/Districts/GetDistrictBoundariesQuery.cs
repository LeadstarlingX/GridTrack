using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Districts;

public sealed record GetDistrictBoundariesQuery();

public sealed class GetDistrictBoundariesHandler
{
    public Task<GetDistrictBoundariesResponse> Handle(
        GetDistrictBoundariesQuery query,
        IDistrictReadService readService,
        CancellationToken ct)
        => readService.GetDistrictBoundariesAsync(ct);
}
