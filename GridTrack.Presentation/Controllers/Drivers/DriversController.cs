using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Drivers;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Drivers;

[ApiController]
[Route("/api/drivers")]
public class DriversController(IMessageBus bus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDrivers(
        [FromQuery] GetDriversRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDriversResponse>(
            new GetDriversQuery(
                request.Cursor,
                request.DistrictId,
                request.Status,
                request.PageSize ?? 8),
            ct);

        return Ok(result);
    }

    [HttpPatch("{id}/availability")]
    public async Task<IActionResult> UpdateDriverAvailability(
        [FromRoute] string id,
        [FromBody] UpdateDriverAvailabilityRequest request,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var driverId))
            return BadRequest();

        var isActive = request.Status.Equals("available", StringComparison.OrdinalIgnoreCase);

        var result = await bus.InvokeAsync<DriverAvailabilityResponse?>(
            new ToggleDriverAvailabilityCommand(driverId, isActive),
            ct);

        return result is null ? NotFound() : Ok(result);
    }
}
