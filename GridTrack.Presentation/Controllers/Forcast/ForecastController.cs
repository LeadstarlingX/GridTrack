using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Forecast;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Forcast;

[ApiController]
[Route("/api/forecast")]
public class ForecastController(IMessageBus bus) : ControllerBase
{
    [HttpGet("{districtId}")]
    public async Task<IActionResult> GetForecast(string districtId, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetForecastResponse?>(
            new GetForecastQuery(districtId),
            ct);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>GET /api/forecast/staffing?districtId=mezzeh&amp;targetAt=2026-06-15T09:00:00Z</summary>
    [HttpGet("staffing")]
    public async Task<IActionResult> GetStaffing(
        [FromQuery] string   districtId,
        [FromQuery] DateTime targetAt,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(districtId))
            return BadRequest(new { message = "districtId is required." });

        var result = await bus.InvokeAsync<StaffingForecastResponse?>(
            new GetStaffingForecastQuery(districtId, targetAt),
            ct);

        return result is null
            ? StatusCode(503, new { code = "AI_UNAVAILABLE", message = "Staffing AI is temporarily unavailable." })
            : Ok(result);
    }
}
