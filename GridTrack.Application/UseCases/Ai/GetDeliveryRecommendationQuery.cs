using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Ai;

public sealed record GetDeliveryRecommendationQuery(Guid DeliveryId);

public sealed class GetDeliveryRecommendationHandler
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<Result<DeliveryRecommendationResponse>> Handle(
        GetDeliveryRecommendationQuery query,
        IDeliveryReadService deliveryReadService,
        IDispatchStrategy strategy,
        IAiRecommendationService aiService,
        ICacheService cache,
        CancellationToken ct)
    {
        var cacheKey = $"recommend:{query.DeliveryId}";
        var cached = await cache.GetAsync<DeliveryRecommendationResponse>(cacheKey, ct);
        if (cached is not null)
            return Result.Success(cached);

        var delivery = await deliveryReadService.GetAggregateByIdAsync(query.DeliveryId, ct);
        if (delivery is null)
            return Result.Failure<DeliveryRecommendationResponse>(ApplicationErrors.DeliveryNotFound);

        var candidates = await strategy.GetCandidatesAsync(delivery.CurrentLocation, count: 3, ct);

        var aiCandidates = candidates
            .Select((c, i) => new AiCandidateContextDto(i + 1, c.Name, c.DistanceM, c.OnTimeRatePct, c.Score))
            .ToList();

        var aiRequest = new AiRecommendationRequestDto(
            delivery.DeliveryId,
            delivery.DistrictId,
            delivery.AnomalyFlag ? delivery.AnomalyTypeValue?.ToString() : null,
            delivery.AnomalyFlag ? delivery.AnomalyReason : null,
            aiCandidates);

        var aiResponse = await aiService.GetAsync(aiRequest, ct);

        Guid? recommendedDriverId = null;
        if (aiResponse is { CandidateRank: not null })
        {
            var idx = aiResponse.CandidateRank.Value - 1;
            if (idx >= 0 && idx < candidates.Count)
                recommendedDriverId = candidates[idx].DriverId;
        }

        var response = new DeliveryRecommendationResponse(
            delivery.DeliveryId,
            delivery.DistrictId,
            candidates,
            aiResponse?.RecommendedAction,
            recommendedDriverId,
            aiResponse?.Reason,
            aiResponse?.UrgencyScore,
            AiAvailable: aiResponse is not null);

        await cache.SetAsync(cacheKey, response, CacheTtl, ct);
        return Result.Success(response);
    }
}
