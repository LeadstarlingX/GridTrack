using GridTrack.Application.Interfaces;
using H3;
using H3.Algorithms;
using H3.Model;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.H3Service;

public sealed class H3GridService : IH3GridService
{
    public Task<string> GetCellIndexForPointAsync(Point location, int resolution)
    {
        if (location is null)
        {
            throw new ArgumentNullException(nameof(location));
        }

        var c = new LatLng{Latitude = location.Y, Longitude = location.X};
        var cell = H3Index.FromLatLng(c, resolution);
        string cellString = cell.ToString();

        return Task.FromResult(cellString);
    }

    public Task<IEnumerable<string?>> GetNeighborCellsAsync(string cellIndex, int ringDistance)
    {
        if (string.IsNullOrWhiteSpace(cellIndex))
        {
            throw new ArgumentException("H3 index cannot be empty.", nameof(cellIndex));
        }

        if (ringDistance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ringDistance), "Ring distance must be positive.");
        }
        
        var cell = new H3Index(cellIndex);
        var neighbors = cell.GridDiskDistances(ringDistance);
        return Task.FromResult(neighbors.Select(n => n.ToString()));
    }

    public Task<IEnumerable<string>> GenerateGridBoundsAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        int resolution)
    {

        var polygon = new Polygon(new LinearRing(new[] {
            new Coordinate((double)minLng, (double)minLat),
            new Coordinate((double)maxLng, (double)minLat),
            new Coordinate((double)maxLng, (double)maxLat),
            new Coordinate((double)minLng, (double)maxLat),
            new Coordinate((double)minLng, (double)minLat)
        }));

        // H3.net supports filling NTS Polygons directly
        var cells = polygon.Fill(resolution);
        return Task.FromResult(cells.Select(c => c.ToString()));
    }
}

// Documentation: https://github.com/pocketken/H3.net