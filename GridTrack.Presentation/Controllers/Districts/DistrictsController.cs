using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Districts;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Districts;

[ApiController]
[Route("api/districts")]
public class DistrictsController(IMessageBus bus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDistricts(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDistrictsResponse>(
            new GetDistrictsQuery(),
            ct);

        return Ok(result.Items);
    }

    [HttpGet("boundaries")]
    public async Task<IActionResult> GetDistrictBoundaries(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDistrictBoundariesResponse>(
            new GetDistrictBoundariesQuery(),
            ct);

        return Ok(result);
    }
}
