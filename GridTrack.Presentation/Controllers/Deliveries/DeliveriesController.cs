using GridTrack.Presentation.Abstractions.Api;
using Microsoft.AspNetCore.Mvc;

namespace GridTrack.Presentation.Controllers.Deliveries;

[ApiController]
[Route("api/deliveries")]
public class DeliveriesController : ControllerBase
{
    // GET: api/deliveries
    [HttpGet]
    public async Task<ActionResult<PagedResponse<DeliveryListItemResponse>>> GetDeliveries(
        [FromQuery] GetDeliveriesRequest request)
    {
        // Implementation for paginated list of deliveries with filtering
        // This would typically call into your application layer
        throw new NotImplementedException();
    }

    // GET: api/deliveries/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<DeliveryDetailResponse>> GetDelivery(string id)
    {
        // Implementation for getting detailed delivery including route polyline
        // This would typically call into your application layer
        throw new NotImplementedException();
    }
}