using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Forecasting;

public sealed record HeatmapRequest(string DistrictId, DateTime Window);

public sealed record GetHeatmapDataQuery(HeatmapRequest Request);

public sealed class GetHeatmapDataHandler
{
    public async Task<Result<IEnumerable<HeatmapPointDto>>> Handle(
        GetHeatmapDataQuery query,
        IHeatmapReadService readService,
        CancellationToken ct)
    {
        var heatmap = await readService.GetHeatmapAsync(query.Request.DistrictId, query.Request.Window, ct);
        return Result.Success(heatmap);
    }
}
