using System.Text.Json;
using GridTrack.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GridTrack.Infrastructure.ExternalServices;

public sealed class DistrictDataService : IDistrictDataService
{
    private readonly IReadOnlyList<DistrictInfo> _districts;

    public DistrictDataService(string geoJsonPath, ILogger<DistrictDataService> logger)
    {
        _districts = Load(geoJsonPath, logger);
        logger.LogInformation("DistrictDataService: loaded {Count} neighborhoods from {Path}", _districts.Count, geoJsonPath);
    }

    public IReadOnlyList<DistrictInfo> GetAll() => _districts;

    public DistrictInfo? GetById(string id)
        => _districts.FirstOrDefault(d => d.Id == id);

    public DistrictInfo GetRandom(string? exceptId = null)
    {
        var pool = exceptId is null
            ? _districts
            : (IReadOnlyList<DistrictInfo>)_districts.Where(d => d.Id != exceptId).ToList();
        if (pool.Count == 0) pool = _districts;
        return pool[Random.Shared.Next(pool.Count)];
    }

    private static IReadOnlyList<DistrictInfo> Load(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            logger.LogWarning("DistrictDataService: GeoJSON not found at {Path} — district list will be empty", path);
            return [];
        }

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var districts = new List<DistrictInfo>();

        foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            try
            {
                var props = feature.GetProperty("properties");

                var osmId = props.GetProperty("osm_id");
                var id = osmId.ValueKind == JsonValueKind.Number
                    ? osmId.GetInt64().ToString()
                    : osmId.GetString() ?? "";

                var nameAr = props.TryGetProperty("name_fixed", out var nf) && nf.ValueKind == JsonValueKind.String
                    ? nf.GetString() ?? id
                    : id;

                var geometry = feature.GetProperty("geometry");
                var geomType = geometry.GetProperty("type").GetString();

                // Get exterior ring coordinates
                JsonElement ring;
                if (geomType == "Polygon")
                    ring = geometry.GetProperty("coordinates")[0];
                else if (geomType == "MultiPolygon")
                    ring = geometry.GetProperty("coordinates")[0][0];
                else
                    continue;

                var coords = ring.EnumerateArray().ToList();
                if (coords.Count == 0) continue;

                var cLng = coords.Average(p => p[0].GetDouble());
                var cLat = coords.Average(p => p[1].GetDouble());
                var maxDist = coords.Max(p =>
                    Math.Sqrt(Math.Pow(p[0].GetDouble() - cLng, 2) + Math.Pow(p[1].GetDouble() - cLat, 2)));
                var jitter = Math.Min(maxDist * 0.35, 0.018);

                districts.Add(new DistrictInfo(id, nameAr, cLat, cLng, jitter));
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "DistrictDataService: skipped one feature");
            }
        }

        return districts.AsReadOnly();
    }
}
