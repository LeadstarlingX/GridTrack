using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetH3DensityQuery(DateTime From, DateTime To, int Resolution, int? FromHour, int? ToHour);

public sealed class GetH3DensityHandler
{
    public Task<GetH3DensityResponse> Handle(
        GetH3DensityQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetH3DensityAsync(query.From, query.To, query.Resolution, query.FromHour, query.ToHour, ct);
}
