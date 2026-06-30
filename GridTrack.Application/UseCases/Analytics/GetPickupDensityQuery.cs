using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetPickupDensityQuery(DateTime From, DateTime To, int? FromHour, int? ToHour);

public sealed class GetPickupDensityHandler
{
    public Task<GetPickupDensityResponse> Handle(
        GetPickupDensityQuery query,
        IAnalyticsReadService readService,
        CancellationToken ct)
        => readService.GetPickupDensityAsync(query.From, query.To, query.FromHour, query.ToHour, ct);
}
