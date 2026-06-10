namespace GridTrack.Presentation.Controllers.Analytics;

/// <summary>Optional created-at range filter shared by range-scoped analytics endpoints.</summary>
public sealed record AnalyticsRangeRequest(DateTime? From, DateTime? To);
