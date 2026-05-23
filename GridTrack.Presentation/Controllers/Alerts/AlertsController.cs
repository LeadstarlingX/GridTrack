using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Alerts;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Alerts;

[ApiController]
[Route("/api/alerts")]
public class AlertsController(IMessageBus bus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] GetAlertsRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetAlertsResponse>(
            new GetAlertsQuery(
                request.Cursor,
                request.From,
                request.To,
                request.DistrictId,
                request.AnomalyType,
                request.PageSize ?? 6),
            ct);

        return Ok(result);
    }
}
