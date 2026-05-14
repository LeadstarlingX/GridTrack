using NetTopologySuite.Geometries;

namespace GridTrack.Application.Interfaces;

public interface IH3GridService
{
    Task<string> GetCellIndexForPointAsync(Point location, int resolution);
    Task<IEnumerable<string?>> GetNeighborCellsAsync(string h3Index, int ringDistance);
    Task<IEnumerable<string>> GenerateGridBoundsAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        int resolution);
}
