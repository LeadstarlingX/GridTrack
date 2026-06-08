using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.IntegrationEvents;

namespace GridTrack.Application.UseCases.Forecast;

public sealed record GetForecastQuery(string DistrictId);

public sealed class GetForecastHandler
{
    public async Task<GetForecastResponse?> Handle(
        GetForecastQuery query,
        ICacheService cache,
        IForecastReadService readService,
        CancellationToken ct)
    {
        // Redis-first: return Python ML result if available
        var cached = await cache.GetAsync<ForecastResultMessage>($"forecast:{query.DistrictId}", ct);
        if (cached is not null)
            return MapFromMessage(cached);

        // DB fallback: count deliveries created within the last hour
        var forecast = await readService.GetForecastAsync(query.DistrictId, DateTime.UtcNow.AddHours(-1), ct);
        if (forecast is null)
            return null;

        var driversNeeded = Math.Max(1, (int)Math.Ceiling(forecast.ExpectedDeliveries / 10.0));
        var staffingRatio = forecast.ExpectedDeliveries > 0
            ? (double)driversNeeded / forecast.ExpectedDeliveries
            : 1.0;

        // Cache DB-derived result for 1 minute so rapid re-reads don't hammer Postgres;
        // Python's async result (5-min TTL) will overwrite this when it arrives.
        var fallbackMessage = new ForecastResultMessage(
            DistrictId: forecast.DistrictId,
            ExpectedDeliveries: forecast.ExpectedDeliveries,
            StaffingRatio: staffingRatio,
            Label: string.Empty,
            Color: string.Empty,
            GeneratedAt: forecast.GeneratedAt);

        await cache.SetAsync($"forecast:{query.DistrictId}", fallbackMessage, TimeSpan.FromMinutes(1), ct);

        return new GetForecastResponse(
            forecast.DistrictId,
            forecast.ExpectedDeliveries,
            Horizon: "next-hour",
            driversNeeded,
            staffingRatio,
            forecast.GeneratedAt);
    }

    private static GetForecastResponse MapFromMessage(ForecastResultMessage msg)
    {
        var driversNeeded = Math.Max(1, (int)Math.Ceiling(msg.ExpectedDeliveries / 10.0));
        var staffingRatio = msg.ExpectedDeliveries > 0
            ? msg.StaffingRatio
            : 1.0;

        return new GetForecastResponse(
            msg.DistrictId,
            msg.ExpectedDeliveries,
            Horizon: "next-hour",
            driversNeeded,
            staffingRatio,
            msg.GeneratedAt);
    }
}
