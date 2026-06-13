using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Application.UseCases.Drivers;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using NetTopologySuite;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Telemetry;

[ApiController]
[Route("/api/telemetry")]
public class TelemetryController(IMessageBus bus) : ControllerBase
{
    private static readonly GeometryFactory GeoFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    [HttpPost("batch")]
    public async Task<IActionResult> IngestBatch(
        [FromBody] TelemetryBatchRequest request,
        CancellationToken ct)
    {
        if (request.Events is not { Count: > 0 })
            return BadRequest(new { message = "Batch must contain at least one event." });

        var processed = 0;
        var rejected  = 0;
        var errors    = new List<string>();

        foreach (var evt in request.Events)
        {
            try
            {
                await DispatchEvent(evt, ct);
                processed++;
            }
            catch (Exception ex)
            {
                rejected++;
                errors.Add($"[{evt.Type ?? "unknown"}] {ex.Message}");
            }
        }

        return Accepted(new { processed, rejected, errors });
    }

    private async Task DispatchEvent(TelemetryEventRequest evt, CancellationToken ct)
    {
        var occurredAt = evt.OccurredAt ?? DateTime.UtcNow;

        switch (evt.Type?.ToLowerInvariant())
        {
            case "position":
                await DispatchPosition(evt, occurredAt, ct);
                break;

            case "delivery_created":
                await DispatchDeliveryCreated(evt, occurredAt, ct);
                break;

            case "delivery_status":
                await DispatchDeliveryStatus(evt, occurredAt, ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown event type '{evt.Type}'.");
        }
    }

    private async Task DispatchPosition(TelemetryEventRequest evt, DateTime occurredAt, CancellationToken ct)
    {
        if (evt.DriverId is null)
            throw new InvalidOperationException("driverId is required for position events.");
        if (evt.Lat is null || evt.Lng is null)
            throw new InvalidOperationException("lat and lng are required for position events.");

        var point = GeoFactory.CreatePoint(new Coordinate(evt.Lng.Value, evt.Lat.Value));
        await bus.InvokeAsync(
            new UpdateDriverPositionCommand(new UpdatePositionRequest(evt.DriverId.Value, point, occurredAt)),
            ct);
    }

    private async Task DispatchDeliveryCreated(TelemetryEventRequest evt, DateTime occurredAt, CancellationToken ct)
    {
        if (evt.Lat is null || evt.Lng is null)
            throw new InvalidOperationException("lat and lng are required for delivery_created events.");

        var point = GeoFactory.CreatePoint(new Coordinate(evt.Lng.Value, evt.Lat.Value));
        await bus.InvokeAsync(
            new CreateDeliveryCommand(new CreateDeliveryRequest(
                evt.DeliveryId ?? Guid.NewGuid(),
                point,
                H3Resolution: 7,
                evt.ExpectedEta,
                evt.DistrictId)),
            ct);
    }

    private async Task DispatchDeliveryStatus(TelemetryEventRequest evt, DateTime occurredAt, CancellationToken ct)
    {
        if (evt.DeliveryId is null)
            throw new InvalidOperationException("deliveryId is required for delivery_status events.");

        var deliveryId = evt.DeliveryId.Value;

        switch (evt.Status?.ToLowerInvariant())
        {
            case "picked_up":
                if (evt.Lat is null || evt.Lng is null)
                    throw new InvalidOperationException("lat and lng are required for picked_up status.");
                var pickupPoint = GeoFactory.CreatePoint(new Coordinate(evt.Lng.Value, evt.Lat.Value));
                await bus.InvokeAsync(
                    new MarkDeliveryPickedUpCommand(new PickUpDeliveryRequest(deliveryId, pickupPoint, occurredAt)),
                    ct);
                break;

            case "delivered":
                await bus.InvokeAsync(
                    new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(deliveryId, occurredAt)),
                    ct);
                break;

            case "cancelled":
                await bus.InvokeAsync(
                    new CancelDeliveryCommand(new CancelDeliveryRequest(
                        deliveryId,
                        occurredAt,
                        evt.CancelReason ?? "Cancelled by driver.")),
                    ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown delivery status '{evt.Status}'. Expected: picked_up, delivered, cancelled.");
        }
    }
}
