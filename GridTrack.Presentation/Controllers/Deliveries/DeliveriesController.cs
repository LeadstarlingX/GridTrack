using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Common;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Deliveries;

[ApiController]
[Route("/api/deliveries")]
public class DeliveriesController(IMessageBus bus) : ControllerBase
{
    private static readonly GeometryFactory GeoFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    // ── Queries ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetDeliveries([FromQuery] GetDeliveriesRequest request, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDeliveriesResponse>(
            new GetDeliveriesQuery(
                request.Cursor, request.Status, request.DistrictId,
                request.From, request.To, request.PageSize ?? 6),
            ct);
        return Ok(result);
    }

    [HttpGet("by-district/{districtId}")]
    public async Task<IActionResult> GetDeliveriesByDistrict(string districtId, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<Result<IEnumerable<DeliveryDto>>>(
            new GetDeliveriesByDistrictQuery(new DistrictFilterRequest(districtId)), ct);
        return Ok(result.Value.Select(DeliverySummaryResponse.From));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDelivery(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();
        var result = await bus.InvokeAsync<GetDeliveryByIdResponse?>(new GetDeliveryByIdQuery(deliveryId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id}/route")]
    public async Task<IActionResult> GetDeliveryRoute(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();
        var result = await bus.InvokeAsync<IEnumerable<RouteWaypointDto>>(new GetDeliveryRouteQuery(deliveryId), ct);
        return Ok(result);
    }

    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> GetDeliveryTimeline(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();
        var result = await bus.InvokeAsync<DeliveryTimelineResponse?>(new GetDeliveryTimelineQuery(deliveryId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [HttpPost("{id}/assign")]
    public async Task<IActionResult> AssignDriver(
        string id, [FromBody] AssignDriverHttpRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();
        var result = await bus.InvokeAsync<Result>(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(deliveryId, request.DriverId)), ct);
        return ResultResponse(result);
    }

    [HttpPost("{id}/pick-up")]
    public async Task<IActionResult> PickUp(
        string id, [FromBody] PickUpDeliveryHttpRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();
        var location = GeoFactory.CreatePoint(new Coordinate(request.Lng, request.Lat));
        var result = await bus.InvokeAsync<Result>(
            new MarkDeliveryPickedUpCommand(new PickUpDeliveryRequest(deliveryId, location, DateTime.UtcNow)), ct);
        return ResultResponse(result);
    }

    [HttpPost("{id}/delivered")]
    public async Task<IActionResult> MarkDelivered(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();
        var result = await bus.InvokeAsync<Result>(
            new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(deliveryId, DateTime.UtcNow)), ct);
        return ResultResponse(result);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(
        string id, [FromBody] CancelDeliveryHttpRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();
        var result = await bus.InvokeAsync<Result>(
            new CancelDeliveryCommand(new CancelDeliveryRequest(deliveryId, DateTime.UtcNow, request.Reason)), ct);
        return ResultResponse(result);
    }

    [HttpPost("{id}/flag-anomaly")]
    public async Task<IActionResult> FlagAnomaly(
        string id, [FromBody] FlagAnomalyHttpRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();
        if (!Enum.TryParse<AnomalyType>(request.Type, ignoreCase: true, out var anomalyType))
            return BadRequest(new { error = $"Unknown anomaly type '{request.Type}'." });
        var result = await bus.InvokeAsync<Result>(
            new FlagDeliveryAnomalyCommand(new FlagAnomalyRequest(deliveryId, anomalyType, request.Reason)), ct);
        return ResultResponse(result);
    }

    private IActionResult ResultResponse(Result result) => result.IsSuccess
        ? NoContent()
        : result.Error == ApplicationErrors.DeliveryNotFound
            ? NotFound(new { error = result.Error.Message })
            : Conflict(new { error = result.Error.Message });
}
