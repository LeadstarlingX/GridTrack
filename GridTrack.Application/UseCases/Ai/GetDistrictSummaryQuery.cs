using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;

namespace GridTrack.Application.UseCases.Ai;

public sealed record GetDistrictSummaryQuery(string DistrictId);

public sealed class GetDistrictSummaryHandler
{
    private static readonly TimeSpan FreshTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StaleTtl = TimeSpan.FromHours(1);

    public async Task<DistrictSummaryResponse?> Handle(
        GetDistrictSummaryQuery query,
        IDistrictReadService districtService,
        IAnalysisChatService chatService,
        ICacheService cache,
        CancellationToken ct)
    {
        var freshKey = $"ai:district-summary:{query.DistrictId}";
        var staleKey = $"ai:district-summary:{query.DistrictId}:stale";

        var cached = await cache.GetAsync<DistrictSummaryResponse>(freshKey, ct);
        if (cached is not null)
            return cached;

        var ctx = await districtService.GetDistrictContextAsync(query.DistrictId, ct);

        var question = $"What is the single most important action for district '{query.DistrictId}' right now?";
        var csvContext = $"active_deliveries={ctx.ActiveDeliveries}," +
                         $"active_drivers={ctx.ActiveDrivers}," +
                         $"anomaly_rate_24h={ctx.AnomalyRate24h:P0}";

        var summary = await chatService.AskAsync(question, csvContext, ct);

        var now = DateTime.UtcNow;
        if (summary is not null)
        {
            var fresh = new DistrictSummaryResponse(query.DistrictId, summary, now, CachedAt: null);
            await cache.SetAsync(freshKey, fresh, FreshTtl, ct);
            await cache.SetAsync(staleKey, fresh, StaleTtl, ct);
            return fresh;
        }

        // AI unavailable — return last known result (stale), or null so callers can surface a 404
        return await cache.GetAsync<DistrictSummaryResponse>(staleKey, ct);
    }
}
