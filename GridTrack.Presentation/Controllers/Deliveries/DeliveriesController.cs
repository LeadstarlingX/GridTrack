using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using Microsoft.AspNetCore.Mvc;
using Wolverine;


namespace GridTrack.Presentation.Controllers.Deliveries;

[ApiController]
[Route("/api/deliveries")]
public class DeliveriesController(IMessageBus bus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDeliveries(
        [FromQuery] GetDeliveriesRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(
                request.Cursor,
                request.Status,
                request.DistrictId,
                request.From,
                request.To,
                request.PageSize ?? 6),
            ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDelivery(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();

        var result = await bus.InvokeAsync<GetDeliveryByIdResponse?>(
            new GetDeliveryByIdQuery(deliveryId),
            ct);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id}/route")]
    public async Task<IActionResult> GetDeliveryRoute(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();

        var result = await bus.InvokeAsync<IEnumerable<RouteWaypointDto>>(
            new GetDeliveryRouteQuery(deliveryId),
            ct);

        return Ok(result);
    }
}
