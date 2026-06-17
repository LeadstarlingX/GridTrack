using GridTrack.Domain.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.Interfaces;

public interface IH3GridService
{
    Task<Result<string>> GetCellAsync(Point location, int resolution);
    Task<Result<IEnumerable<string>>> GetGridDiskAsync(string h3Index, int ringDistance);
    Task<IEnumerable<string>> FillBoundingBoxAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        int resolution);
}
