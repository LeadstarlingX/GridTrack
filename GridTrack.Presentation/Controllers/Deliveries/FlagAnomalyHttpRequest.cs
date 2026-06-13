namespace GridTrack.Presentation.Controllers.Deliveries;

// Valid Type values: EtaExceeded, RouteDeviation, StalePosition, UnexpectedStop
public sealed record FlagAnomalyHttpRequest(string Type, string Reason);
