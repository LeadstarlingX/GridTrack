using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Analytics;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Analytics;

[ApiController]
[Route("/api/analytics")]
public class AnalyticsController(IMessageBus bus) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetAnalyticsSummaryResponse>(
            new GetAnalyticsSummaryQuery(),
            ct);

        return Ok(result);
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends(
        [FromQuery] GetTrendsRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetTrendsResponse>(
            new GetTrendsQuery(request.From, request.To, request.Granularity),
            ct);

        return Ok(result);
    }

    [HttpGet("h3-density")]
    public async Task<IActionResult> GetH3Density(
        [FromQuery] GetH3DensityRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetH3DensityResponse>(
            new GetH3DensityQuery(
                request.From,
                request.To,
                request.Resolution,
                request.FromHour,
                request.ToHour),
            ct);

        return Ok(result);
    }
}
