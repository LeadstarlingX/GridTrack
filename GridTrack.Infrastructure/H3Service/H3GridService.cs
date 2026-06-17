using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using H3;
using H3.Algorithms;
using H3.Extensions;
using H3.Model;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GridTrack.Infrastructure.H3Service;

public sealed class H3GridService : IH3GridService
{
    public Task<Result<string>> GetCellAsync(Point location, int resolution)
    {
        if (location is null)
            return Task.FromResult(Result.Failure<string>(H3ServiceErrors.LocationNotProvided));

        var cell = new Coordinate(location.X, location.Y).ToH3Index(resolution);
        return Task.FromResult(Result.Success(cell.ToString()));
    }

    public Task<Result<IEnumerable<string>>> GetGridDiskAsync(string cellIndex, int ringDistance)
    {
        if (string.IsNullOrWhiteSpace(cellIndex))
            return Task.FromResult(Result.Failure<IEnumerable<string>>(H3ServiceErrors.InvalidCellIndex));

        if (ringDistance <= 0)
            return Task.FromResult(Result.Failure<IEnumerable<string>>(H3ServiceErrors.InvalidRingDistance));

        var cell = new H3Index(cellIndex);
        var neighbors = cell.GridDiskDistances(ringDistance)
            .Where(n => n.Distance > 0)
            .Select(n => n.Index.ToString());
        return Task.FromResult(Result.Success<IEnumerable<string>>(neighbors));
    }

    public Task<IEnumerable<string>> FillBoundingBoxAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        int resolution)
    {
        
        if (Math.Abs(minLat - maxLat) < 1e-6 && Math.Abs(minLng - maxLng) < 1e-6)
        {
            var latLng = new Coordinate(minLat, minLng);
            var cell = latLng.ToH3Index(resolution);
            return Task.FromResult<IEnumerable<string>>(new List<string> { cell.ToString() });
        }
        
        // Standard GIS: X = Longitude, Y = Latitude
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