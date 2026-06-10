namespace GridTrack.Application.Dtos;

public sealed record CancellationReasonItemResponse(string Reason, int Count);

public sealed record GetCancellationAnalyticsResponse(
    int TotalCancelled,
    int LateCancellations,
    double CancellationRate,
    IReadOnlyList<CancellationReasonItemResponse> Reasons);
