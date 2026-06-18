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
    public async Task<IActionResult> GetSummary(
        [FromQuery] AnalyticsRangeRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetAnalyticsSummaryResponse>(
            new GetAnalyticsSummaryQuery(request.From, request.To),
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

    [HttpGet("district-volume")]
    public async Task<IActionResult> GetDistrictVolume(
        [FromQuery] AnalyticsRangeRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDistrictVolumeResponse>(
            new GetDistrictVolumeQuery(request.From, request.To),
            ct);

        return Ok(result);
    }

    [HttpGet("cancellations")]
    public async Task<IActionResult> GetCancellationAnalytics(
        [FromQuery] AnalyticsRangeRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetCancellationAnalyticsResponse>(
            new GetCancellationAnalyticsQuery(request.From, request.To),
            ct);

        return Ok(result);
    }

    [HttpGet("delivery-performance")]
    public async Task<IActionResult> GetDeliveryPerformance(
        [FromQuery] AnalyticsRangeRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDeliveryPerformanceResponse>(
            new GetDeliveryPerformanceQuery(request.From, request.To),
            ct);

        return Ok(result);
    }

    [HttpGet("driver-utilization")]
    public async Task<IActionResult> GetDriverUtilization(
        [FromQuery] int top,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDriverUtilizationResponse>(
            new GetDriverUtilizationQuery(top <= 0 ? 10 : Math.Min(top, 50)),
            ct);

        return Ok(result);
    }

    [HttpGet("status-breakdown")]
    public async Task<IActionResult> GetStatusBreakdown(
        [FromQuery] AnalyticsRangeRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetStatusBreakdownResponse>(
            new GetStatusBreakdownQuery(request.From, request.To),
            ct);

        return Ok(result);
    }

    [HttpGet("anomaly-breakdown")]
    public async Task<IActionResult> GetAnomalyBreakdown(
        [FromQuery] AnalyticsRangeRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetAnomalyBreakdownResponse>(
            new GetAnomalyBreakdownQuery(request.From, request.To),
            ct);

        return Ok(result);
    }

    [HttpGet("drivers")]
    public async Task<IActionResult> GetDriverAnalytics(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDriverAnalyticsResponse>(
            new GetDriverAnalyticsQuery(),
            ct);

        return Ok(result);
    }
}
