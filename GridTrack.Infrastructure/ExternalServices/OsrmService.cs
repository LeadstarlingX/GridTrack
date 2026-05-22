using System.Net.Http.Json;
using System.Text.Json;
using GridTrack.Application.Interfaces;

namespace GridTrack.Infrastructure.ExternalServices;

public sealed class OsrmService(HttpClient http) : IOsrmService
{
    public async Task<OsrmRouteResult?> GetRouteAsync(
        double originLat, double originLng,
        double destLat, double destLng,
        CancellationToken ct = default)
    {
        // OSRM uses lng,lat order
        var url = $"/route/v1/driving/{originLng},{originLat};{destLng},{destLat}" +
                  "?overview=full&geometries=geojson&steps=false";

        var json = await http.GetFromJsonAsync<JsonElement>(url, ct);
        var route = json.GetProperty("routes")[0];
        var duration = route.GetProperty("duration").GetDouble();
        var distance = route.GetProperty("distance").GetDouble();
        var coords = route.GetProperty("geometry").GetProperty("coordinates");

        var waypoints = new List<(double, double)>();
        foreach (var coord in coords.EnumerateArray())
            waypoints.Add((coord[1].GetDouble(), coord[0].GetDouble())); // geojson = [lng, lat]

        return new OsrmRouteResult(waypoints, duration, distance);
    }
}
