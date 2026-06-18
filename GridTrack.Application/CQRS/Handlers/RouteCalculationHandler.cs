using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Dispatch;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Deliveries;
using Microsoft.Extensions.Logging;

namespace GridTrack.Application.CQRS.Handlers;

public static class RouteCalculationHandler
{
    private sealed record LocationRow(double Lat, double Lng);

    public static async Task Handle(
        RouteCalculationMessage msg,
        ISqlConnectionFactory sql,
        IOsrmService osrm,
        IRouteCostCalculator costCalculator,
        ILogger<RouteCalculationMessage> logger,
        CancellationToken ct)
    {
        using var conn = sql.CreateConnection();

        var driver = await conn.QueryFirstOrDefaultAsync<LocationRow>(
            """
            SELECT ST_Y("Location"::geometry) AS "Lat",
                   ST_X("Location"::geometry) AS "Lng"
            FROM "Drivers"
            WHERE "DriverId" = @Id
            """,
            new { Id = msg.DriverId });

        var delivery = await conn.QueryFirstOrDefaultAsync<LocationRow>(
            """
            SELECT ST_Y("CurrentLocation"::geometry) AS "Lat",
                   ST_X("CurrentLocation"::geometry) AS "Lng"
            FROM "Deliveries"
            WHERE "DeliveryId" = @Id
            """,
            new { Id = msg.DeliveryId });

        if (driver is null || delivery is null) return;

        var route = await osrm.GetRouteAsync(driver.Lat, driver.Lng, delivery.Lat, delivery.Lng, ct);
        if (route is null)
        {
            logger.LogWarning("OSRM returned no route for delivery {DeliveryId}", msg.DeliveryId);
            return;
        }

        await conn.ExecuteAsync(
            """DELETE FROM delivery_routes WHERE "DeliveryId" = @Id""",
            new { Id = msg.DeliveryId });

        var waypoints = route.Waypoints
            .Select((wp, i) => new { DeliveryId = msg.DeliveryId, Seq = i, wp.Lat, wp.Lng })
            .ToList();

        await conn.ExecuteAsync(
            """
            INSERT INTO delivery_routes ("DeliveryId", "Sequence", "Lat", "Lng")
            VALUES (@DeliveryId, @Seq, @Lat, @Lng)
            """,
            waypoints);

        // Persist route economics on the delivery so the dashboard can show cost.
        var cost = costCalculator.Calculate(route.DistanceMeters, route.DurationSeconds);
        await conn.ExecuteAsync(
            """
            UPDATE "Deliveries"
            SET "RouteDistanceMeters" = @Distance,
                "RouteDurationSeconds" = @Duration,
                "RouteCost" = @Cost
            WHERE "DeliveryId" = @Id
            """,
            new { Distance = route.DistanceMeters, Duration = route.DurationSeconds, Cost = cost, Id = msg.DeliveryId });

        logger.LogInformation(
            "Route calculated for delivery {DeliveryId}: {Count} waypoints, {Km:F1} km, {Cost} SYP",
            msg.DeliveryId, waypoints.Count, route.DistanceMeters / 1000.0, cost);
    }
}
