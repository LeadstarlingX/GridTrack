using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Drivers;
using Microsoft.Extensions.Logging;

namespace GridTrack.Application.CQRS.Handlers;

public static class RecalculateDeliveryEtaHandler
{
    // Finds the delivery that this driver is actively moving toward.
    // Status integers: 2 = PickedUp, 3 = InTransit.
    private sealed record ActiveDeliveryRow(
        Guid DeliveryId,
        string DistrictId,
        double DestLat,
        double DestLng);

    public static async Task Handle(
        RecalculateDeliveryEtaMessage msg,
        ISqlConnectionFactory sql,
        IOsrmService osrm,
        IDeliveryReadService deliveryReadService,
        IDashboardPushService push,
        ILogger<RecalculateDeliveryEtaMessage> logger,
        CancellationToken ct)
    {
        using var conn = sql.CreateConnection();

        var row = await conn.QueryFirstOrDefaultAsync<ActiveDeliveryRow>(
            """
            SELECT d."DeliveryId",
                   d."DistrictId",
                   ST_Y(d."CurrentLocation"::geometry) AS "DestLat",
                   ST_X(d."CurrentLocation"::geometry) AS "DestLng"
            FROM   "Deliveries" d
            WHERE  d."AssignedDriverId" = @DriverId
              AND  d."Status" IN (2, 3)
            LIMIT  1
            """,
            new { DriverId = msg.DriverId });

        if (row is null) return;

        var route = await osrm.GetRouteAsync(msg.DriverLat, msg.DriverLng, row.DestLat, row.DestLng, ct);
        if (route is null)
        {
            logger.LogDebug("OSRM unavailable for ETA recalc, driver {DriverId}", msg.DriverId);
            return;
        }

        var expectedEta = DateTime.UtcNow.AddSeconds(route.DurationSeconds);

        await conn.ExecuteAsync(
            """UPDATE "Deliveries" SET "ExpectedEta" = @Eta WHERE "DeliveryId" = @Id""",
            new { Eta = expectedEta, Id = row.DeliveryId });

        // Read the full aggregate so the broadcast payload is complete.
        var delivery = await deliveryReadService.GetAggregateByIdAsync(row.DeliveryId, ct);
        if (delivery is null) return;

        await push.BroadcastDeliveryUpdateAsync(
            delivery.DistrictId,
            new DeliveryDto
            {
                DeliveryId           = delivery.DeliveryId,
                CurrentLocation      = delivery.CurrentLocation,
                Status               = delivery.Status,
                AssignedDriverId     = delivery.AssignedDriverId,
                ExpectedEta          = expectedEta,   // use freshly computed value, not DB round-trip
                ActualEta            = delivery.ActualEta,
                DistrictId           = delivery.DistrictId,
                AnomalyFlag          = delivery.AnomalyFlag,
                CreatedAt            = delivery.CreatedAt,
                PickedUpAt           = delivery.PickedUpAt,
                DeliveredAt          = delivery.DeliveredAt,
                CancelledAt          = delivery.CancelledAt,
                AnomalyReason        = delivery.AnomalyReason,
                RouteDistanceMeters  = delivery.RouteDistanceMeters,
                RouteDurationSeconds = delivery.RouteDurationSeconds,
                RouteCost            = delivery.RouteCost,
            },
            ct);

        logger.LogDebug(
            "ETA recalculated for delivery {DeliveryId}: {Secs:F0}s remaining",
            row.DeliveryId, route.DurationSeconds);
    }
}
