using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;

namespace GridTrack.Infrastructure.ExternalServices;

internal sealed class PythonAiRecommendationService(HttpClient http) : IAiRecommendationService
{
    public async Task<AiRecommendationResponse?> GetAsync(AiRecommendationRequest request, CancellationToken ct)
    {
        try
        {
            var body = new
            {
                delivery_id   = request.DeliveryId.ToString(),
                district_id   = request.DistrictId,
                anomaly_type  = request.AnomalyType,
                anomaly_reason = request.AnomalyReason,
                candidates    = request.Candidates.Select(c => new
                {
                    rank             = c.Rank,
                    name             = c.Name,
                    distance_m       = c.DistanceM,
                    on_time_rate_pct = c.OnTimeRatePct,
                    score            = c.Score,
                }).ToList(),
            };

            var response = await http.PostAsJsonAsync("/recommend", body, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<PythonRecommendResponse>(
                cancellationToken: ct);

            return result is null ? null : new AiRecommendationResponse(
                result.RecommendedAction,
                result.CandidateRank,
                result.Reason,
                result.UrgencyScore);
        }
        catch
        {
            return null;
        }
    }

    private sealed record PythonRecommendResponse(
        [property: JsonPropertyName("recommended_action")] string RecommendedAction,
        [property: JsonPropertyName("candidate_rank")]     int?   CandidateRank,
        [property: JsonPropertyName("reason")]             string Reason,
        [property: JsonPropertyName("urgency_score")]      int    UrgencyScore);
}
