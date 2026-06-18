using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IAiRecommendationService
{
    Task<AiRecommendationResponse?> GetAsync(AiRecommendationRequestDto request, CancellationToken ct);
}
