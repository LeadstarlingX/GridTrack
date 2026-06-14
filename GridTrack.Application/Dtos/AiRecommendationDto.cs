namespace GridTrack.Application.Dtos;

public sealed record AiCandidateContext(
    int    Rank,
    string Name,
    double DistanceM,
    double? OnTimeRatePct,
    double Score);

public sealed record AiRecommendationRequest(
    Guid    DeliveryId,
    string  DistrictId,
    string? AnomalyType,
    string? AnomalyReason,
    IReadOnlyList<AiCandidateContext> Candidates);

public sealed record AiRecommendationResponse(
    string RecommendedAction,  // Reassign | Contact | Cancel | Monitor
    int?   CandidateRank,
    string Reason,
    int    UrgencyScore);

public sealed record DeliveryRecommendationResponse(
    Guid   DeliveryId,
    string DistrictId,
    IReadOnlyList<DispatchCandidateDto> TopCandidates,
    // AI section — all null when AI is unavailable
    string? RecommendedAction,
    Guid?   RecommendedDriverId,
    string? Reason,
    int?    UrgencyScore,
    bool    AiAvailable);
