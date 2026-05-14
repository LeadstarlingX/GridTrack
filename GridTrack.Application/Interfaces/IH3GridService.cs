using NetTopologySuite.Geometries;

namespace GridTrack.Application.Interfaces;

public interface IH3GridService
{
    Task<string> GetCellIndexForPointAsync(Point location, int resolution);
    Task<IEnumerable<string?>> GetNeighborCellsAsync(string h3Index, int ringDistance);
    Task<IEnumerable<string>> GenerateGridBoundsAsync(
        decimal minLat,
        decimal maxLat,
        decimal minLng,
        decimal maxLng,
        int resolution);
}
