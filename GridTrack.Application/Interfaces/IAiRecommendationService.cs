using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IAiRecommendationService
{
    /// <summary>
    /// Calls the Python recommendation service.
    /// Returns null when the service is unavailable (network error, timeout, non-2xx).
    /// </summary>
    Task<AiRecommendationResponse?> GetAsync(AiRecommendationRequestDto request, CancellationToken ct);
}
