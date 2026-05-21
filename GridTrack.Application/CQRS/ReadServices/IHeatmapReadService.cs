using GridTrack.Application.Dtos;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IHeatmapReadService
{
    Task<IEnumerable<HeatmapPointDto>> GetHeatmapAsync(string districtId, DateTime window, CancellationToken ct);
}
