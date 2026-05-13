using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IHeatmapReadService
{
    Task<IEnumerable<HeatmapPointDto>> GetHeatmapAsync(string districtId, DateTime window, CancellationToken ct);
}
