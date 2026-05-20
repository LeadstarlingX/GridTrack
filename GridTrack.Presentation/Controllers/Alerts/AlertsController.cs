using GridTrack.Presentation.Abstractions.Api;
using Microsoft.AspNetCore.Mvc;

namespace GridTrack.Presentation.Controllers.Alerts;

[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    // GET: api/alerts
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AnomalyAlertResponse>>> GetAlerts(
        [FromQuery] GetAlertsRequest request)
    {
        // Implementation for paginated anomaly alerts with filtering
        // This would typically call into your application layer
        throw new NotImplementedException();
    }
}