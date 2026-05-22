using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Forecast;

public sealed record GetForecastQuery(string DistrictId);

public sealed class GetForecastHandler
{
    public async Task<GetForecastResponse?> Handle(
        GetForecastQuery query,
        IForecastReadService readService,
        CancellationToken ct)
    {
        // Look back 24 h of actual deliveries as the demand basis for the next 24-h forecast window
        var forecast = await readService.GetForecastAsync(query.DistrictId, DateTime.UtcNow.AddHours(-24), ct);
        if (forecast is null)
            return null;

        var driversNeeded = Math.Max(1, (int)Math.Ceiling(forecast.ExpectedDeliveries / 10.0));
        var staffingRatio = forecast.ExpectedDeliveries > 0
            ? (double)driversNeeded / forecast.ExpectedDeliveries
            : 1.0;

        return new GetForecastResponse(
            forecast.DistrictId,
            forecast.ExpectedDeliveries,
            Horizon: "24h",
            driversNeeded,
            staffingRatio,
            forecast.GeneratedAt);
    }
}
