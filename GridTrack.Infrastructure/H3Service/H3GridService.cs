using GridTrack.Application.Interfaces;
using H3;
using H3.Algorithms;
using H3.Extensions;
using H3.Model;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GridTrack.Infrastructure.H3Service;

public sealed class H3GridService : IH3GridService
{
    public Task<string> GetCellAsync(Point location, int resolution)
    {
        if (location is null)
        {
            throw new ArgumentNullException(nameof(location));
        }

        var latLng = new Coordinate(location.X, location.Y);
        var cell = latLng.ToH3Index(resolution);
        string cellString = cell.ToString();

        return Task.FromResult(cellString);
    }

    public Task<IEnumerable<string>> GetGridDiskAsync(string cellIndex, int ringDistance)
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
        var neighbors = cell.GridDiskDistances(ringDistance)
            .Where(n => n.Distance > 0) // exclude origin
            .Select(n => n.Index.ToString());
        return Task.FromResult(neighbors);
    }

    public Task<IEnumerable<string>> FillBoundingBoxAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        int resolution)
    {
        
        // 1. Envelope takes (minX, maxX, minY, maxY) 
        // 2. Standard GIS: X = Longitude, Y = Latitude
        var envelope = new Envelope(minLng, maxLng, minLat, maxLat);
    
        var polygon = (Polygon)new GeometryFactory().ToGeometry(envelope);

        var cells = polygon.Fill(resolution);

        var cellStrings = cells
            .Select(c => c.ToString()) 
            .ToList(); 

        return Task.FromResult<IEnumerable<string>>(cellStrings);
        
    }
}

// Documentation: https://github.com/pocketken/H3.net