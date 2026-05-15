using NetTopologySuite.Geometries;

namespace GridTrack.Application.Interfaces;

public interface IH3GridService
{
    Task<string> GetCellAsync(Point location, int resolution);
    Task<IEnumerable<string>> GetGridDiskAsync(string h3Index, int ringDistance);
    Task<IEnumerable<string>> FillBoundingBoxAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        int resolution);
}
