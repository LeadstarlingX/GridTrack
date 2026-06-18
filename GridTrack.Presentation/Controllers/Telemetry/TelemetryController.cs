using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Telemetry;

[ApiController]
[Route("/api/telemetry")]
public class TelemetryController(IMessageBus bus, ISqlConnectionFactory sqlFactory) : ControllerBase
{
    private static readonly GeometryFactory GeoFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    [HttpPost("position")]
    public async Task<IActionResult> UpdatePosition(
        [FromBody] TelemetryPositionRequest request, CancellationToken ct)
    {
        var location = GeoFactory.CreatePoint(new Coordinate(request.Lng, request.Lat));
        var result = await bus.InvokeAsync<Result>(
            new UpdateDriverPositionCommand(new UpdatePositionRequest(
                request.DriverId, location, request.Timestamp ?? DateTime.UtcNow)), ct);
        if (result.IsFailure)
            return result.Error == ApplicationErrors.DriverNotFound
                ? NotFound(new { error = result.Error.Message })
                : UnprocessableEntity(new { error = result.Error.Message });
        return NoContent();
    }

    // Sync baseline: direct per-request Postgres UPDATE, bypassing the write-behind buffer.
    // Used for architecture comparison — shows the latency cost without the buffer optimization.
    [HttpPost("position/sync")]
    public async Task<IActionResult> UpdatePositionSync(
        [FromBody] TelemetryPositionRequest request, CancellationToken ct)
    {
        using var conn = sqlFactory.CreateConnection();
        var rows = await conn.ExecuteAsync(
            """
            UPDATE "Drivers"
            SET "Location" = ST_SetSRID(ST_MakePoint(@lng, @lat), 4326),
                "LastSeen"  = @ts
            WHERE "DriverId" = @id
            """,
            new { id = request.DriverId, lat = request.Lat, lng = request.Lng, ts = request.Timestamp ?? DateTime.UtcNow });
        return rows == 0 ? NotFound(new { error = "Driver not found." }) : NoContent();
    }

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
            var error = await DispatchEvent(evt, ct);
            if (error is null)
                processed++;
            else
            {
                rejected++;
                errors.Add($"[{evt.Type ?? "unknown"}] {error}");
            }
        }

        return Accepted(new { processed, rejected, errors });
    }

    private async Task<string?> DispatchEvent(TelemetryEventRequest evt, CancellationToken ct)
    {
        var occurredAt = evt.OccurredAt ?? DateTime.UtcNow;

        return evt.Type?.ToLowerInvariant() switch
        {
            "position"        => await DispatchPosition(evt, occurredAt, ct),
            "delivery_created" => await DispatchDeliveryCreated(evt, occurredAt, ct),
            "delivery_status"  => await DispatchDeliveryStatus(evt, occurredAt, ct),
            _                  => $"Unknown event type '{evt.Type}'.",
        };
    }

    private async Task<string?> DispatchPosition(TelemetryEventRequest evt, DateTime occurredAt, CancellationToken ct)
    {
        if (evt.DriverId is null)
            return "driverId is required for position events.";
        if (evt.Lat is null || evt.Lng is null)
            return "lat and lng are required for position events.";

        var point = GeoFactory.CreatePoint(new Coordinate(evt.Lng.Value, evt.Lat.Value));
        await bus.InvokeAsync(
            new UpdateDriverPositionCommand(new UpdatePositionRequest(evt.DriverId.Value, point, occurredAt)),
            ct);
        return null;
    }

    private async Task<string?> DispatchDeliveryCreated(TelemetryEventRequest evt, DateTime occurredAt, CancellationToken ct)
    {
        if (evt.Lat is null || evt.Lng is null)
            return "lat and lng are required for delivery_created events.";

        var point = GeoFactory.CreatePoint(new Coordinate(evt.Lng.Value, evt.Lat.Value));
        await bus.InvokeAsync(
            new CreateDeliveryCommand(new CreateDeliveryRequest(
                evt.DeliveryId ?? Guid.NewGuid(),
                point,
                H3Resolution: 7,
                evt.ExpectedEta,
                evt.DistrictId)),
            ct);
        return null;
    }

    private async Task<string?> DispatchDeliveryStatus(TelemetryEventRequest evt, DateTime occurredAt, CancellationToken ct)
    {
        if (evt.DeliveryId is null)
            return "deliveryId is required for delivery_status events.";

        var deliveryId = evt.DeliveryId.Value;

        switch (evt.Status?.ToLowerInvariant())
        {
            case "picked_up":
                if (evt.Lat is null || evt.Lng is null)
                    return "lat and lng are required for picked_up status.";
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
                return $"Unknown delivery status '{evt.Status}'. Expected: picked_up, delivered, cancelled.";
        }

        return null;
    }
}
