using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Abstractions;
using GridTrack.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.Simulation;


public sealed class PositionSimulatorService(
    ISqlConnectionFactory sqlFactory,
    IHubContext<DashboardHub> hub,
    IOsrmService osrm,
    IServiceScopeFactory scopeFactory,
    IOptions<SimulatorOptions> options,
    ILogger<PositionSimulatorService> logger) : BackgroundService
{
    private enum DeliveryPhase { Patrol, MovingToPickup, MovingToDropoff }

    private sealed record SimDriver(
        Guid Id, string Name, string ShortName, string DistrictId, bool IsActive,
        double CenterLat, double CenterLng,
        IReadOnlyList<(double Lat, double Lng)> Waypoints,
        int WaypointIndex, int Direction,
        DateTime LastBroadcastAt,
        DateTime? PausedUntil,
        bool StallBroadcastSent,
        Guid? ActiveDeliveryId,
        DeliveryPhase Phase);

    private sealed record SimDelivery(Guid Id, double Lat, double Lng, string DistrictId);

    private static readonly (double DLat, double DLng)[] DestOffsets =
    [
        ( 0.005,  0.000), ( 0.000,  0.005), (-0.005,  0.000), ( 0.000, -0.005),
        ( 0.003,  0.004), (-0.004,  0.003), ( 0.004, -0.003), (-0.003, -0.004),
    ];

    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);

    private List<SimDriver> _drivers = [];
    private readonly Queue<SimDelivery> _pendingDeliveries = new();
    private readonly HashSet<Guid> _activeDeliveryIds = [];
    private int _tickCount;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = options.Value;
        if (!opts.Enabled) { logger.LogInformation("PositionSimulator disabled"); return; }

        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        await LoadDriversAsync(ct);
        await LoadPendingDeliveriesAsync(ct);

        if (_drivers.Count == 0) { logger.LogWarning("PositionSimulator: no active drivers"); return; }

        logger.LogInformation("PositionSimulator started — {Count} drivers, {Interval} ms interval, {Deliveries} pending deliveries",
            _drivers.Count, opts.PositionUpdateIntervalMs, _pendingDeliveries.Count);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(opts.PositionUpdateIntervalMs, ct);
            _tickCount++;
            if (_tickCount % opts.DeliveryReloadIntervalTicks == 0)
                await LoadPendingDeliveriesAsync(ct);
            await TickAsync(ct);
        }
    }

    // ── Driver/delivery loading ──────────────────────────────────────────────

    private async Task LoadDriversAsync(CancellationToken ct)
    {
        try
        {
            using var conn = sqlFactory.CreateConnection();
            const string sql = """
                SELECT "DriverId" AS Id, "Name", "ShortName", "DistrictId",
                       ST_Y("Location") AS CenterLat, ST_X("Location") AS CenterLng, "IsActive"
                FROM public."Drivers" WHERE "IsActive" = true
                """;
            var rows = (await conn.QueryAsync<DriverRow>(sql)).ToList();
            var drivers = new List<SimDriver>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var waypoints = await FetchRouteWaypointsAsync(r.CenterLat, r.CenterLng, i, ct);
                var start = waypoints.Count > 1 ? i % waypoints.Count : 0;
                drivers.Add(new SimDriver(
                    r.Id, r.Name, r.ShortName, r.DistrictId, r.IsActive,
                    r.CenterLat, r.CenterLng,
                    Waypoints: waypoints, WaypointIndex: start, Direction: 1,
                    LastBroadcastAt: DateTime.UtcNow,
                    PausedUntil: null, StallBroadcastSent: false,
                    ActiveDeliveryId: null, Phase: DeliveryPhase.Patrol));
            }
            _drivers = drivers;
        }
        catch (Exception ex) { logger.LogError(ex, "PositionSimulator: failed to load drivers"); }
    }

    private async Task LoadPendingDeliveriesAsync(CancellationToken ct)
    {
        try
        {
            using var conn = sqlFactory.CreateConnection();
            const string sql = """
                SELECT "DeliveryId" AS Id,
                       ST_Y("CurrentLocation") AS Lat,
                       ST_X("CurrentLocation") AS Lng,
                       "DistrictId"
                FROM public."Deliveries"
                WHERE "Status" = 0
                  AND "AssignedDriverId" IS NULL
                ORDER BY "CreatedAt"
                LIMIT 50
                """;
            var rows = await conn.QueryAsync<DeliveryRow>(sql);
            var fresh = rows
                .Where(r => !_activeDeliveryIds.Contains(r.Id))
                .Select(r => new SimDelivery(r.Id, r.Lat, r.Lng, r.DistrictId));

            foreach (var d in fresh)
                if (!_pendingDeliveries.Any(p => p.Id == d.Id))
                    _pendingDeliveries.Enqueue(d);
        }
        catch (Exception ex) { logger.LogError(ex, "PositionSimulator: failed to load deliveries"); }
    }

    // ── Tick ────────────────────────────────────────────────────────────────

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var opts = options.Value;
        var broadcastTasks = new List<Task>(_drivers.Count);

        for (var i = 0; i < _drivers.Count; i++)
        {
            var d = _drivers[i];
            if (d.Waypoints.Count == 0) continue;

            // ── Stall logic ───────────────────────────────────────────────
            if (d.PausedUntil.HasValue && d.PausedUntil.Value > now)
            {
                if (!d.StallBroadcastSent &&
                    (now - d.LastBroadcastAt).TotalSeconds > opts.StallThresholdSeconds)
                {
                    _drivers[i] = d with { StallBroadcastSent = true };
                    broadcastTasks.Add(hub.Clients.All.SendCoreAsync("StallDetected",
                        [new { driverId = d.Id, driverName = d.Name, districtId = d.DistrictId, stalledSince = d.LastBroadcastAt }], ct));
                }
                continue;
            }

            d = d with { PausedUntil = null, StallBroadcastSent = false };

            // ── Delivery assignment (patrol only) ─────────────────────────
            if (d.Phase == DeliveryPhase.Patrol &&
                Random.Shared.Next(100) < opts.DeliveryAssignProbabilityPct &&
                _pendingDeliveries.Count > 0)
            {
                var delivery = _pendingDeliveries.Dequeue();
                _activeDeliveryIds.Add(delivery.Id);
                var (curLat, curLng) = d.Waypoints[d.WaypointIndex];
                var pickupWaypoints = await FetchRouteWaypointsAsync(curLat, curLng, delivery.Lat, delivery.Lng, ct);

                await InvokeCommandAsync(
                    new AssignDriverToDeliveryCommand(new AssignDriverRequest(delivery.Id, d.Id)), ct);

                _drivers[i] = d with
                {
                    ActiveDeliveryId = delivery.Id,
                    Phase = DeliveryPhase.MovingToPickup,
                    Waypoints = pickupWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    LastBroadcastAt = now,
                };
                logger.LogDebug("Driver {Name} assigned delivery {Id}", d.Name, delivery.Id);
                continue;
            }

            // ── Arrived at pickup → switch to dropoff ─────────────────────
            if (d.Phase == DeliveryPhase.MovingToPickup &&
                IsAtEnd(d.WaypointIndex, d.Direction, d.Waypoints.Count))
            {
                var (pickLat, pickLng) = d.Waypoints[^1];
                await InvokeCommandAsync(
                    new MarkDeliveryPickedUpCommand(new PickUpDeliveryRequest(
                        d.ActiveDeliveryId!.Value,
                        Geo.CreatePoint(new Coordinate(pickLng, pickLat)),
                        now)), ct);

                // Dropoff = jittered point ~0.5 km from pickup
                var angle = Random.Shared.NextDouble() * Math.PI * 2;
                var dropLat = pickLat + 0.004 * Math.Sin(angle);
                var dropLng = pickLng + 0.004 * Math.Cos(angle);
                var dropWaypoints = await FetchRouteWaypointsAsync(pickLat, pickLng, dropLat, dropLng, ct);

                // ETA = route duration × buffer, written directly so domain can detect late cancellation
                var etaSecs = dropWaypoints.Count * opts.PositionUpdateIntervalMs / 1000.0 * opts.EtaBufferMultiplier;
                await SetDeliveryEtaAsync(d.ActiveDeliveryId!.Value, now.AddSeconds(etaSecs), ct);

                _drivers[i] = d with
                {
                    Phase = DeliveryPhase.MovingToDropoff,
                    Waypoints = dropWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    LastBroadcastAt = now,
                };
                logger.LogDebug("Driver {Name} picked up delivery {Id}, heading to dropoff", d.Name, d.ActiveDeliveryId);
                continue;
            }

            // ── Random cancellation in transit ────────────────────────────
            if (d.Phase == DeliveryPhase.MovingToDropoff &&
                Random.Shared.Next(100) < opts.CancellationProbabilityPct)
            {
                await InvokeCommandAsync(
                    new CancelDeliveryCommand(new CancelDeliveryRequest(
                        d.ActiveDeliveryId!.Value, now, "Customer cancelled in transit")), ct);

                _activeDeliveryIds.Remove(d.ActiveDeliveryId!.Value);
                var patrolWaypoints = await FetchRouteWaypointsAsync(d.CenterLat, d.CenterLng, d.CenterLat, d.CenterLng, ct);
                _drivers[i] = d with
                {
                    ActiveDeliveryId = null,
                    Phase = DeliveryPhase.Patrol,
                    Waypoints = patrolWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    LastBroadcastAt = now,
                };
                logger.LogDebug("Driver {Name} cancelled delivery", d.Name);
                continue;
            }

            // ── Arrived at dropoff → complete ─────────────────────────────
            if (d.Phase == DeliveryPhase.MovingToDropoff &&
                IsAtEnd(d.WaypointIndex, d.Direction, d.Waypoints.Count))
            {
                await InvokeCommandAsync(
                    new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(
                        d.ActiveDeliveryId!.Value, now)), ct);

                _activeDeliveryIds.Remove(d.ActiveDeliveryId!.Value);
                var patrolWaypoints = await FetchRouteWaypointsAsync(d.CenterLat, d.CenterLng, d.CenterLat, d.CenterLng, ct);
                _drivers[i] = d with
                {
                    ActiveDeliveryId = null,
                    Phase = DeliveryPhase.Patrol,
                    Waypoints = patrolWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    LastBroadcastAt = now,
                };
                logger.LogDebug("Driver {Name} completed delivery", d.Name);
                continue;
            }

            // ── Stall: only applies to drivers actively moving to dropoff ──
            if (d.Phase == DeliveryPhase.MovingToDropoff &&
                Random.Shared.Next(100) < opts.StallPauseProbabilityPct)
            {
                _drivers[i] = d with { PausedUntil = now.AddSeconds(opts.StallPauseDurationSeconds) };
                continue;
            }

            var (lat, lng) = d.Waypoints[d.WaypointIndex];
            var (nextIdx, nextDir) = AdvanceIndex(d.WaypointIndex, d.Direction, d.Waypoints.Count);
            _drivers[i] = d with { WaypointIndex = nextIdx, Direction = nextDir, LastBroadcastAt = now };

            var routeAhead = d.Phase != DeliveryPhase.Patrol
                ? d.Waypoints.Skip(d.WaypointIndex).Take(40)
                    .Select(static w => new[] { w.Lat, w.Lng })
                    .ToArray()
                : null;

            broadcastTasks.Add(hub.Clients.All.SendCoreAsync("DriverPositionUpdated",
                [new {
                    driverId   = d.Id,
                    lat, lng,
                    districtId = d.DistrictId,
                    deliveryId = d.ActiveDeliveryId?.ToString(),
                    routeAhead,
                }], ct));
        }

        try { await Task.WhenAll(broadcastTasks); }
        catch (Exception ex) { logger.LogWarning(ex, "PositionSimulator: broadcast error"); }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (int nextIdx, int nextDir) AdvanceIndex(int idx, int dir, int count)
    {
        var next = idx + dir;
        if (next >= count) return (count - 2, -1);
        if (next < 0) return (1, 1);
        return (next, dir);
    }

    private static bool IsAtEnd(int idx, int dir, int count)
        => (dir == 1 && idx >= count - 1) || (dir == -1 && idx <= 0);

    private async Task<IReadOnlyList<(double Lat, double Lng)>> FetchRouteWaypointsAsync(
        double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            var route = await osrm.GetRouteAsync(fromLat, fromLng, toLat, toLng, cts.Token);
            if (route?.Waypoints is { Count: > 1 } pts)
            {
                var fwd = pts.ToList();
                var rev = fwd.AsEnumerable().Reverse().Skip(1).ToList();
                var combined = new List<(double, double)>(fwd.Count + rev.Count);
                combined.AddRange(fwd);
                combined.AddRange(rev);
                return combined;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PositionSimulator: OSRM failed, using circular fallback");
        }
        return BuildCircularFallback(fromLat, fromLng);
    }

    // Overload for patrol — uses DestOffsets jitter
    private async Task<IReadOnlyList<(double Lat, double Lng)>> FetchRouteWaypointsAsync(
        double centerLat, double centerLng, int driverIndex, CancellationToken ct)
    {
        var (dLat, dLng) = DestOffsets[driverIndex % DestOffsets.Length];
        return await FetchRouteWaypointsAsync(centerLat, centerLng, centerLat + dLat, centerLng + dLng, ct);
    }

    private static IReadOnlyList<(double Lat, double Lng)> BuildCircularFallback(double lat, double lng)
    {
        const double radius = 0.002;
        const int steps = 60;
        var pts = new List<(double, double)>(steps);
        for (var i = 0; i < steps; i++)
        {
            var a = 2 * Math.PI * i / steps;
            pts.Add((lat + radius * Math.Sin(a), lng + radius * Math.Cos(a)));
        }
        return pts;
    }

    private async Task InvokeCommandAsync<T>(T command, CancellationToken ct) where T : notnull
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
            await bus.InvokeAsync(command, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PositionSimulator: command {Type} failed", typeof(T).Name);
        }
    }

    private async Task SetDeliveryEtaAsync(Guid deliveryId, DateTime eta, CancellationToken ct)
    {
        try
        {
            using var conn = sqlFactory.CreateConnection();
            await ((System.Data.IDbConnection)conn).ExecuteAsync(
                """UPDATE public."Deliveries" SET "ExpectedEta" = @Eta WHERE "DeliveryId" = @Id""",
                new { Eta = eta, Id = deliveryId });
        }
        catch (Exception ex) { logger.LogWarning(ex, "PositionSimulator: ETA update failed"); }
    }

    private sealed class DriverRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string DistrictId { get; set; } = string.Empty;
        public double CenterLat { get; set; }
        public double CenterLng { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class DeliveryRow
    {
        public Guid Id { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string DistrictId { get; set; } = string.Empty;
    }
}
