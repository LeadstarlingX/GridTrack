using System.Text.Json;
using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class DistrictReadService : IDistrictReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public DistrictReadService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<GetDistrictsResponse> GetDistrictsAsync(CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "H3Index"                           AS "Id",
                               ST_Y("CenterPoint"::geometry)::float AS "Lat",
                               ST_X("CenterPoint"::geometry)::float AS "Lng"
                           FROM public."H3District"
                           ORDER BY "H3Index"
                           """;

        var rows = await connection.QueryAsync<DistrictFlatRow>(sql);

        var items = rows
            .Select(r => new DistrictItemResponse(r.Id, r.Id, new CoordinateResponse(r.Lat, r.Lng)))
            .ToList();

        return new GetDistrictsResponse(items);
    }

    public async Task<GetDistrictBoundariesResponse> GetDistrictBoundariesAsync(CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "H3Index",
                               ST_AsGeoJSON("BoundaryPolygon")::text AS "GeoJson"
                           FROM public."H3District"
                           ORDER BY "H3Index"
                           """;

        var rows = await connection.QueryAsync<DistrictBoundaryRow>(sql);

        var features = rows
            .Select(r => BuildFeature(r.H3Index, r.GeoJson))
            .Where(f => f is not null)
            .Select(f => f!)
            .ToList();

        return new GetDistrictBoundariesResponse("FeatureCollection", features);
    }

    private static GeoJsonDistrictFeatureResponse? BuildFeature(string h3Index, string geoJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(geoJson);
            var root = doc.RootElement;

            var type = root.GetProperty("type").GetString() ?? "Polygon";

            // Normalise to MultiPolygon coordinates format
            IReadOnlyList<IReadOnlyList<IReadOnlyList<double[]>>> multiCoords;

            if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase))
            {
                var rings = ParsePolygonCoordinates(root.GetProperty("coordinates"));
                multiCoords = new List<IReadOnlyList<IReadOnlyList<double[]>>> { rings };
            }
            else if (type.Equals("MultiPolygon", StringComparison.OrdinalIgnoreCase))
            {
                multiCoords = ParseMultiPolygonCoordinates(root.GetProperty("coordinates"));
            }
            else
            {
                return null;
            }

            var geometry = new GeoJsonMultiPolygonGeometryResponse("MultiPolygon", multiCoords);
            var properties = new GeoJsonDistrictPropertiesResponse(h3Index, h3Index);

            return new GeoJsonDistrictFeatureResponse("Feature", properties, geometry);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<IReadOnlyList<double[]>> ParsePolygonCoordinates(JsonElement coordsEl)
    {
        var rings = new List<IReadOnlyList<double[]>>();
        foreach (var ring in coordsEl.EnumerateArray())
        {
            var points = ring.EnumerateArray()
                .Select(pt =>
                {
                    var arr = pt.EnumerateArray().Select(v => v.GetDouble()).ToArray();
                    return arr;
                })
                .ToList();
            rings.Add(points);
        }
        return rings;
    }

    private static IReadOnlyList<IReadOnlyList<IReadOnlyList<double[]>>> ParseMultiPolygonCoordinates(JsonElement coordsEl)
    {
        var polys = new List<IReadOnlyList<IReadOnlyList<double[]>>>();
        foreach (var poly in coordsEl.EnumerateArray())
            polys.Add(ParsePolygonCoordinates(poly));
        return polys;
    }

    public async Task<DistrictContextDto> GetDistrictContextAsync(string districtId, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string deliverySql = """
            SELECT
                COUNT(*) FILTER (WHERE "Status" NOT IN (4, 5, 6))::int AS "ActiveDeliveries",
                COUNT(*) FILTER (WHERE "CreatedAt" >= NOW() - INTERVAL '24 hours')::float AS "Total24h",
                COUNT(*) FILTER (
                    WHERE "AnomalyFlag" = true AND "CreatedAt" >= NOW() - INTERVAL '24 hours'
                )::float AS "Anomalous24h"
            FROM public."Deliveries"
            WHERE "DistrictId" = @DistrictId
            """;

        const string driverSql = """
            SELECT COUNT(*)::int FROM public."Drivers"
            WHERE "DistrictId" = @DistrictId AND "IsActive" = true
            """;

        var delivRow = await connection.QuerySingleAsync<DeliveryContextRow>(
            deliverySql, new { DistrictId = districtId });
        var activeDrivers = await connection.ExecuteScalarAsync<int>(
            driverSql, new { DistrictId = districtId });

        var anomalyRate = delivRow.Total24h > 0
            ? delivRow.Anomalous24h / delivRow.Total24h
            : 0.0;

        return new DistrictContextDto(districtId, delivRow.ActiveDeliveries, activeDrivers, anomalyRate);
    }

    // ── Private DTOs for Dapper mapping ────────────────────────────────────
    private sealed record DistrictFlatRow(string Id, double Lat, double Lng);
    private sealed record DistrictBoundaryRow(string H3Index, string GeoJson);
    private sealed record DeliveryContextRow(int ActiveDeliveries, double Total24h, double Anomalous24h);
}
