using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Forecast;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Forcast;

[ApiController]
[Route("api/forecast")]
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
}
