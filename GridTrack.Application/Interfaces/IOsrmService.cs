namespace GridTrack.Application.Interfaces;

public interface IOsrmService
{
    Task<OsrmRouteResult?> GetRouteAsync(
        double originLat, double originLng,
        double destLat, double destLng,
        CancellationToken ct = default);
}

public record OsrmRouteResult(
    IReadOnlyList<(double Lat, double Lng)> Waypoints,
    double DurationSeconds,
    double DistanceMeters);
