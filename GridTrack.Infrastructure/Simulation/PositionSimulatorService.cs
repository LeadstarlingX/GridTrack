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
    IDistrictDataService districts,
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
        DeliveryPhase Phase,
        int CancelAtWaypoint,
        int StallAtWaypoint,
        DateTime? DwellUntil);

    private sealed record SimDelivery(Guid Id, double Lat, double Lng, string DistrictId);

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
            {
                await LoadPendingDeliveriesAsync(ct);
                if (_pendingDeliveries.Count < 30)
                    await SpawnPendingDeliveriesAsync(ct);
            }
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
                var waypoints = await FetchPatrolRouteAsync(r.CenterLat, r.CenterLng, i, ct);
                var start = waypoints.Count > 1 ? i % waypoints.Count : 0;
                drivers.Add(new SimDriver(
                    r.Id, r.Name, r.ShortName, r.DistrictId, r.IsActive,
                    r.CenterLat, r.CenterLng,
                    Waypoints: waypoints, WaypointIndex: start, Direction: 1,
                    LastBroadcastAt: DateTime.UtcNow,
                    PausedUntil: null, StallBroadcastSent: false,
                    ActiveDeliveryId: null, Phase: DeliveryPhase.Patrol,
                    CancelAtWaypoint: -1, StallAtWaypoint: -1, DwellUntil: null));
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
                LIMIT 100
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

    private async Task SpawnPendingDeliveriesAsync(CancellationToken ct)
    {
        try
        {
            using var conn = sqlFactory.CreateConnection();
            const int batch = 50;
            var now = DateTime.UtcNow;
            for (var i = 0; i < batch; i++)
            {
                var d = districts.GetRandom();
                var lat = d.CentroidLat + (Random.Shared.NextDouble() * 2 - 1) * d.JitterRadius;
                var lng = d.CentroidLng + (Random.Shared.NextDouble() * 2 - 1) * d.JitterRadius;
                var createdAt = now.AddSeconds(-Random.Shared.Next(30, 180));
                var eta = createdAt.AddMinutes(20 + Random.Shared.Next(40));
                await conn.ExecuteAsync(
                    """
                    INSERT INTO public."Deliveries"
                        ("DeliveryId", "CurrentLocation", "Status", "DistrictId", "CreatedAt", "ExpectedEta", "AnomalyFlag")
                    VALUES
                        (@Id, ST_SetSRID(ST_MakePoint(@Lng, @Lat), 4326), 0, @DistrictId, @CreatedAt, @ExpectedEta, false)
                    """,
                    new { Id = Guid.NewGuid(), Lng = lng, Lat = lat, DistrictId = d.Id, CreatedAt = createdAt, ExpectedEta = eta });
            }
            logger.LogInformation("PositionSimulator: spawned {Count} pending deliveries", batch);
            await LoadPendingDeliveriesAsync(ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "PositionSimulator: delivery spawn failed"); }
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

            // ── Stall (PausedUntil) ───────────────────────────────────────
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

            // ── Post-delivery dwell ───────────────────────────────────────
            if (d.DwellUntil.HasValue && d.DwellUntil.Value > now) continue;
            if (d.DwellUntil.HasValue)
                d = d with { DwellUntil = null, LastBroadcastAt = now };

            // ── Patrol refresh when loop completes ────────────────────────
            if (d.Phase == DeliveryPhase.Patrol && d.WaypointIndex == 0 && d.Direction == -1)
            {
                var (curLat, curLng) = d.Waypoints[0];
                var freshWaypoints = await FetchPatrolRouteAsync(curLat, curLng, ct);
                _drivers[i] = d with { Waypoints = freshWaypoints, WaypointIndex = 0, Direction = 1, LastBroadcastAt = now };
                continue;
            }

            // ── Delivery assignment (patrol only) ─────────────────────────
            if (d.Phase == DeliveryPhase.Patrol &&
                Random.Shared.Next(100) < opts.DeliveryAssignProbabilityPct &&
                _pendingDeliveries.Count > 0)
            {
                var delivery = _pendingDeliveries.Dequeue();
                _activeDeliveryIds.Add(delivery.Id);
                var (curLat, curLng) = d.Waypoints[d.WaypointIndex];
                var pickupWaypoints = await FetchRouteWaypointsAsync(curLat, curLng, delivery.Lat, delivery.Lng, ct);

                // Roll once: will this driver cancel before reaching pickup?
                var cancelBeforePickup = Random.Shared.Next(100) < opts.PrePickupCancellationProbabilityPct;
                var cancelAt = cancelBeforePickup
                    ? Random.Shared.Next(5, Math.Max(6, pickupWaypoints.Count - 5))
                    : -1;

                await InvokeCommandAsync(new AssignDriverToDeliveryCommand(new AssignDriverRequest(delivery.Id, d.Id)), ct);

                _drivers[i] = d with
                {
                    ActiveDeliveryId = delivery.Id,
                    Phase = DeliveryPhase.MovingToPickup,
                    Waypoints = pickupWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    CancelAtWaypoint = cancelAt,
                    StallAtWaypoint = -1,
                    LastBroadcastAt = now,
                };
                logger.LogDebug("Driver {Name} assigned delivery {Id}", d.Name, delivery.Id);
                continue;
            }

            // ── Pre-pickup cancellation ───────────────────────────────────
            if (d.Phase == DeliveryPhase.MovingToPickup &&
                d.CancelAtWaypoint >= 0 && d.WaypointIndex >= d.CancelAtWaypoint)
            {
                await InvokeCommandAsync(new CancelDeliveryCommand(new CancelDeliveryRequest(
                    d.ActiveDeliveryId!.Value, now, "Customer cancelled before pickup")), ct);

                _activeDeliveryIds.Remove(d.ActiveDeliveryId!.Value);
                var (curLat, curLng) = d.Waypoints[d.WaypointIndex];
                var patrolWaypoints = await FetchPatrolRouteAsync(curLat, curLng, ct);
                _drivers[i] = d with
                {
                    ActiveDeliveryId = null,
                    Phase = DeliveryPhase.Patrol,
                    Waypoints = patrolWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    CancelAtWaypoint = -1,
                    StallAtWaypoint = -1,
                    LastBroadcastAt = now,
                };
                logger.LogDebug("Driver {Name} cancelled before pickup", d.Name);
                continue;
            }

            // ── Arrived at pickup → switch to dropoff ─────────────────────
            if (d.Phase == DeliveryPhase.MovingToPickup &&
                IsAtEnd(d.WaypointIndex, d.Direction, d.Waypoints.Count))
            {
                var (pickLat, pickLng) = d.Waypoints[^1];
                await InvokeCommandAsync(new MarkDeliveryPickedUpCommand(new PickUpDeliveryRequest(
                    d.ActiveDeliveryId!.Value,
                    Geo.CreatePoint(new Coordinate(pickLng, pickLat)),
                    now)), ct);

                // Cross-district dropoff
                var dropDistrict = districts.GetRandom(d.DistrictId);
                var dropLat = dropDistrict.CentroidLat + (Random.Shared.NextDouble() * 2 - 1) * dropDistrict.JitterRadius;
                var dropLng = dropDistrict.CentroidLng + (Random.Shared.NextDouble() * 2 - 1) * dropDistrict.JitterRadius;
                var dropWaypoints = await FetchRouteWaypointsAsync(pickLat, pickLng, dropLat, dropLng, ct);

                // Roll once: will this transit have a stall and/or a cancellation?
                var willStall = Random.Shared.Next(100) < opts.StallPauseProbabilityPct;
                var willCancelTransit = Random.Shared.Next(100) < opts.CancellationProbabilityPct;
                var stallAt = willStall
                    ? Random.Shared.Next(10, Math.Max(11, dropWaypoints.Count / 2))
                    : -1;
                var cancelTransitAt = willCancelTransit
                    ? Random.Shared.Next(dropWaypoints.Count / 3,
                        Math.Max(dropWaypoints.Count / 3 + 1, dropWaypoints.Count - 10))
                    : -1;
                // Ensure stall fires before cancellation when both are set
                if (stallAt >= 0 && cancelTransitAt >= 0 && stallAt >= cancelTransitAt)
                    stallAt = Math.Max(0, cancelTransitAt / 2);

                var etaSecs = dropWaypoints.Count * opts.PositionUpdateIntervalMs / 1000.0 * opts.EtaBufferMultiplier;
                await SetDeliveryEtaAsync(d.ActiveDeliveryId!.Value, now.AddSeconds(etaSecs), ct);

                _drivers[i] = d with
                {
                    Phase = DeliveryPhase.MovingToDropoff,
                    Waypoints = dropWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    CancelAtWaypoint = cancelTransitAt,
                    StallAtWaypoint = stallAt,
                    LastBroadcastAt = now,
                };
                logger.LogDebug("Driver {Name} picked up, heading to {District}", d.Name, dropDistrict.Id);
                continue;
            }

            // ── Planned stall during transit ──────────────────────────────
            if (d.Phase == DeliveryPhase.MovingToDropoff &&
                d.StallAtWaypoint >= 0 && d.WaypointIndex >= d.StallAtWaypoint)
            {
                _drivers[i] = d with
                {
                    PausedUntil = now.AddSeconds(opts.StallPauseDurationSeconds),
                    StallAtWaypoint = -1,
                };
                continue;
            }

            // ── Planned cancellation during transit ───────────────────────
            if (d.Phase == DeliveryPhase.MovingToDropoff &&
                d.CancelAtWaypoint >= 0 && d.WaypointIndex >= d.CancelAtWaypoint)
            {
                await InvokeCommandAsync(new CancelDeliveryCommand(new CancelDeliveryRequest(
                    d.ActiveDeliveryId!.Value, now, "Customer cancelled in transit")), ct);

                _activeDeliveryIds.Remove(d.ActiveDeliveryId!.Value);
                var (curLat, curLng) = d.Waypoints[d.WaypointIndex];
                var patrolWaypoints = await FetchPatrolRouteAsync(curLat, curLng, ct);
                var dwellSecs = opts.DwellMinSeconds + Random.Shared.Next(Math.Max(1, opts.DwellMaxSeconds - opts.DwellMinSeconds));
                _drivers[i] = d with
                {
                    ActiveDeliveryId = null,
                    Phase = DeliveryPhase.Patrol,
                    Waypoints = patrolWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    CancelAtWaypoint = -1,
                    StallAtWaypoint = -1,
                    DwellUntil = now.AddSeconds(dwellSecs),
                    LastBroadcastAt = now,
                };
                logger.LogDebug("Driver {Name} cancelled in transit", d.Name);
                continue;
            }

            // ── Arrived at dropoff → complete ─────────────────────────────
            if (d.Phase == DeliveryPhase.MovingToDropoff &&
                IsAtEnd(d.WaypointIndex, d.Direction, d.Waypoints.Count))
            {
                await InvokeCommandAsync(new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(
                    d.ActiveDeliveryId!.Value, now)), ct);

                _activeDeliveryIds.Remove(d.ActiveDeliveryId!.Value);
                var (dropLat2, dropLng2) = d.Waypoints[^1];
                var patrolWaypoints = await FetchPatrolRouteAsync(dropLat2, dropLng2, ct);
                var dwellSecs = opts.DwellMinSeconds + Random.Shared.Next(Math.Max(1, opts.DwellMaxSeconds - opts.DwellMinSeconds));
                _drivers[i] = d with
                {
                    ActiveDeliveryId = null,
                    Phase = DeliveryPhase.Patrol,
                    Waypoints = patrolWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    CancelAtWaypoint = -1,
                    StallAtWaypoint = -1,
                    DwellUntil = now.AddSeconds(dwellSecs),
                    LastBroadcastAt = now,
                };
                logger.LogDebug("Driver {Name} completed delivery", d.Name);
                continue;
            }

            // ── Advance position and broadcast ────────────────────────────
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

    // Initial patrol: golden angle spread so drivers start in different directions
    private async Task<IReadOnlyList<(double Lat, double Lng)>> FetchPatrolRouteAsync(
        double centerLat, double centerLng, int driverIndex, CancellationToken ct)
    {
        var angle = (driverIndex * 2.399963) % (2 * Math.PI);
        var dist = 0.005 + Random.Shared.NextDouble() * 0.004;
        var destLat = centerLat + dist * Math.Sin(angle);
        var destLng = centerLng + dist * Math.Cos(angle);
        return await FetchRouteWaypointsAsync(centerLat, centerLng, destLat, destLng, ct);
    }

    // Subsequent patrol refresh: fully random direction
    private async Task<IReadOnlyList<(double Lat, double Lng)>> FetchPatrolRouteAsync(
        double currentLat, double currentLng, CancellationToken ct)
    {
        var angle = Random.Shared.NextDouble() * 2 * Math.PI;
        var dist = 0.005 + Random.Shared.NextDouble() * 0.004;
        var destLat = currentLat + dist * Math.Sin(angle);
        var destLng = currentLng + dist * Math.Cos(angle);
        return await FetchRouteWaypointsAsync(currentLat, currentLng, destLat, destLng, ct);
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
