using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.ValueObjects;
using GridTrack.Infrastructure.Hubs;
using GridTrack.Infrastructure.Seeding;
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
    IDistrictGroupCache districtGroupCache,
    IOptions<SimulatorOptions> options,
    SeedCompletionSignal seedSignal,
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
        // Route deviation: driver broadcasts an offset position for DeviationTicksLeft ticks
        int RouteDeviationWaypoint,
        int DeviationTicksLeft,
        double DeviationLatOffset,
        double DeviationLngOffset,
        DateTime? DwellUntil,
        DateTime LastEtaRefreshAt,
        bool IsAggressiveStaller);

    private sealed record SimDelivery(Guid Id, double Lat, double Lng, string DistrictId);

    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);

    private static readonly Meter SimulatorMeter = new("gridtrack.simulator", "1.0");
    private static readonly Histogram<double> TickDuration =
        SimulatorMeter.CreateHistogram<double>("simulator.tick.duration", "ms", "Time to compute and broadcast one position tick");
    private static readonly Histogram<int> BatchSize =
        SimulatorMeter.CreateHistogram<int>("simulator.batch.size", "{drivers}", "Driver positions broadcast per tick");

    private List<SimDriver> _drivers = [];
    private readonly Queue<SimDelivery> _pendingDeliveries = new();
    private readonly HashSet<Guid> _activeDeliveryIds = [];
    private int _tickCount;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = options.Value;
        if (!opts.Enabled) { logger.LogInformation("PositionSimulator disabled"); return; }

        // Wait for seeding to actually finish — every startup now does a full reseed (~20s with
        // OSRM calls), and loading drivers before it settles reads an empty or half-written
        // table. Capped so a hung/failed seed can't block the simulator forever.
        try
        {
            using var seedWaitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            seedWaitCts.CancelAfter(TimeSpan.FromSeconds(60));
            await seedSignal.Completed.WaitAsync(seedWaitCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("PositionSimulator: seed did not complete within 60s, starting anyway");
        }

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
                    CancelAtWaypoint: -1, StallAtWaypoint: -1,
                    RouteDeviationWaypoint: -1, DeviationTicksLeft: 0,
                    DeviationLatOffset: 0, DeviationLngOffset: 0,
                    DwellUntil: null, LastEtaRefreshAt: DateTime.MinValue,
                    IsAggressiveStaller: i < 3));
            }
            _drivers = drivers;

            // Cancel all stale non-terminal deliveries left over from previous simulator runs.
            // Without this, old assigned/in-transit deliveries pollute the driver status query.
            if (drivers.Count > 0)
            {
                var ids = drivers.Select(d => d.Id).ToArray();
                var cancelled = await conn.ExecuteAsync(
                    """
                    UPDATE public."Deliveries"
                    SET "Status" = 5, "CancelledAt" = @Now
                    WHERE "AssignedDriverId" = ANY(@Ids)
                      AND "Status" NOT IN (4, 5)
                    """,
                    new { Now = DateTime.UtcNow, Ids = ids });
                if (cancelled > 0)
                    logger.LogInformation("PositionSimulator: cancelled {Count} stale deliveries on startup", cancelled);
            }
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
                // PickupLocation = CurrentLocation for pending deliveries (pickup hasn't happened yet;
                // the domain's MarkPickedUp command will carry the real position through EF)
                await conn.ExecuteAsync(
                    """
                    INSERT INTO public."Deliveries"
                        ("DeliveryId", "CurrentLocation", "PickupLocation", "Status", "DistrictId", "CreatedAt", "ExpectedEta", "AnomalyFlag")
                    VALUES
                        (@Id, ST_SetSRID(ST_MakePoint(@Lng, @Lat), 4326),
                             ST_SetSRID(ST_MakePoint(@Lng, @Lat), 4326),
                         0, @DistrictId, @CreatedAt, @ExpectedEta, false)
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
        var sw = Stopwatch.StartNew();
        var now = DateTime.UtcNow;
        var opts = options.Value;
        var broadcastTasks = new List<Task>(_drivers.Count);
        var positionBatch = new List<object>(_drivers.Count);
        var batchByDistrict = new Dictionary<string, List<object>>();

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
                    var stallPayload = new { driverId = d.Id, driverName = d.Name, districtId = d.DistrictId, stalledSince = d.LastBroadcastAt };
                    var stallGroupIds = await districtGroupCache.GetGroupIdsForDistrictAsync(d.DistrictId, ct);
                    foreach (var sgid in stallGroupIds)
                        broadcastTasks.Add(hub.Clients.Group($"dg:{sgid}").SendCoreAsync("StallDetected", [stallPayload], ct));
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
                // Keep at least 2 drivers freely patrolling for demo visibility
                var freePatrolCount = _drivers.Count(x =>
                    x.Phase == DeliveryPhase.Patrol &&
                    !x.PausedUntil.HasValue &&
                    (!x.DwellUntil.HasValue || x.DwellUntil.Value <= now));
                if (freePatrolCount <= 2) goto advancePosition;

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
                    RouteDeviationWaypoint = -1,
                    DeviationTicksLeft = 0,
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

                // Cross-district dropoff
                var dropDistrict = districts.GetRandom(d.DistrictId);
                var dropLat = dropDistrict.CentroidLat + (Random.Shared.NextDouble() * 2 - 1) * dropDistrict.JitterRadius;
                var dropLng = dropDistrict.CentroidLng + (Random.Shared.NextDouble() * 2 - 1) * dropDistrict.JitterRadius;
                var dropWaypoints = await FetchRouteWaypointsAsync(pickLat, pickLng, dropLat, dropLng, ct);

                // Roll once: stall, cancellation, and route deviation for this transit leg
                // Aggressive stallers (first 3 drivers) stall almost always and very early
                var stallProb = d.IsAggressiveStaller ? 90 : opts.StallPauseProbabilityPct;
                var willStall = Random.Shared.Next(100) < stallProb;
                var willCancelTransit = Random.Shared.Next(100) < opts.CancellationProbabilityPct;
                var stallAt = willStall
                    ? (d.IsAggressiveStaller
                        ? Random.Shared.Next(2, Math.Max(3, dropWaypoints.Count / 4))
                        : Random.Shared.Next(10, Math.Max(11, dropWaypoints.Count / 2)))
                    : -1;
                var cancelTransitAt = willCancelTransit
                    ? Random.Shared.Next(dropWaypoints.Count / 3,
                        Math.Max(dropWaypoints.Count / 3 + 1, dropWaypoints.Count - 10))
                    : -1;
                // Ensure stall fires before cancellation when both are set
                if (stallAt >= 0 && cancelTransitAt >= 0 && stallAt >= cancelTransitAt)
                    stallAt = Math.Max(0, cancelTransitAt / 2);
                // Route deviation: driver leaves optimal path for DeviationDurationTicks ticks
                var willDeviate = Random.Shared.Next(100) < opts.RouteDeviationProbabilityPct;
                var deviateAt = willDeviate
                    ? Random.Shared.Next(15, Math.Max(16, dropWaypoints.Count * 2 / 3))
                    : -1;
                var devLatOff = willDeviate ? (Random.Shared.NextDouble() * 2 - 1) * opts.RouteDeviationRadiusDeg : 0;
                var devLngOff = willDeviate ? (Random.Shared.NextDouble() * 2 - 1) * opts.RouteDeviationRadiusDeg : 0;

                var etaSecs = dropWaypoints.Count * opts.PositionUpdateIntervalMs / 1000.0 * opts.EtaBufferMultiplier;
                var etaTime = now.AddSeconds(etaSecs);

                // FIX: Write ETA to DB BEFORE firing the status-change command.
                await SetDeliveryEtaAsync(d.ActiveDeliveryId!.Value, etaTime, ct);
                logger.LogInformation("[SIM] Wrote ETA {Secs}s for delivery {Id} BEFORE pickup command", etaSecs, d.ActiveDeliveryId);

                await InvokeCommandAsync(new MarkDeliveryPickedUpCommand(new PickUpDeliveryRequest(
                    d.ActiveDeliveryId!.Value,
                    Geo.CreatePoint(new Coordinate(pickLng, pickLat)),
                    now)), ct);
                // Advance delivery from PickedUp → InTransit so driver shows as "in-transit" in the API
                await InvokeCommandAsync(new UpdateDeliveryLocationCommand(new UpdateLocationRequest(
                    d.ActiveDeliveryId!.Value,
                    Geo.CreatePoint(new Coordinate(pickLng, pickLat)),
                    now)), ct);
                logger.LogInformation("[SIM] Pickup command fired for delivery {Id}", d.ActiveDeliveryId);

                _drivers[i] = d with
                {
                    Phase = DeliveryPhase.MovingToDropoff,
                    Waypoints = dropWaypoints,
                    WaypointIndex = 0,
                    Direction = 1,
                    CancelAtWaypoint = cancelTransitAt,
                    StallAtWaypoint = stallAt,
                    RouteDeviationWaypoint = deviateAt,
                    DeviationTicksLeft = 0,
                    DeviationLatOffset = devLatOff,
                    DeviationLngOffset = devLngOff,
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
                if (d.ActiveDeliveryId.HasValue)
                    await InvokeCommandAsync(new FlagDeliveryAnomalyCommand(new FlagAnomalyRequest(
                        d.ActiveDeliveryId.Value, AnomalyType.UnexpectedStop, "Driver stalled during transit")), ct);
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
                    RouteDeviationWaypoint = -1,
                    DeviationTicksLeft = 0,
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
                    RouteDeviationWaypoint = -1,
                    DeviationTicksLeft = 0,
                    DwellUntil = now.AddSeconds(dwellSecs),
                    LastBroadcastAt = now,
                };
                // Broadcast immediately as available so the frontend doesn't show in-transit during dwell
                var dropEntry = new { driverId = d.Id, lat = dropLat2, lng = dropLng2, districtId = d.DistrictId, deliveryId = (string?)null, routeAhead = (object?)null };
                positionBatch.Add(dropEntry);
                if (!batchByDistrict.TryGetValue(d.DistrictId, out var dropBucket))
                    batchByDistrict[d.DistrictId] = dropBucket = [];
                dropBucket.Add(dropEntry);
                logger.LogDebug("Driver {Name} completed delivery", d.Name);
                continue;
            }

            // ── Route deviation onset — anomaly event only, dot stays on route ──
            if (d.Phase == DeliveryPhase.MovingToDropoff &&
                d.RouteDeviationWaypoint >= 0 && d.WaypointIndex >= d.RouteDeviationWaypoint)
            {
                d = d with { RouteDeviationWaypoint = -1, DeviationTicksLeft = 0 };
                var wp = d.Waypoints[d.WaypointIndex];
                var anomalyPayload = new
                {
                    id          = $"sim-dev-{Guid.NewGuid():N}",
                    deliveryId  = d.ActiveDeliveryId?.ToString() ?? Guid.NewGuid().ToString(),
                    driverId    = d.Id.ToString(),
                    driverName  = d.Name,
                    anomalyType = "RouteDeviation",
                    reason      = "Driver deviated from assigned route",
                    districtId  = d.DistrictId,
                    lat         = wp.Lat,
                    lng         = wp.Lng,
                    timestamp   = now,
                };
                var anomalyGroupIds = await districtGroupCache.GetGroupIdsForDistrictAsync(d.DistrictId, ct);
                foreach (var agid in anomalyGroupIds)
                    broadcastTasks.Add(hub.Clients.Group($"dg:{agid}").SendCoreAsync("AnomalyBroadcast", [anomalyPayload], ct));
                if (d.ActiveDeliveryId.HasValue)
                    await InvokeCommandAsync(new FlagDeliveryAnomalyCommand(new FlagAnomalyRequest(
                        d.ActiveDeliveryId.Value, AnomalyType.RouteDeviation, "Driver deviated from assigned route")), ct);
            }

            // ── Advance position and broadcast ────────────────────────────
            advancePosition:
            var (lat, lng) = d.Waypoints[d.WaypointIndex];
            var (nextIdx, nextDir) = AdvanceIndex(d.WaypointIndex, d.Direction, d.Waypoints.Count);

            // ── ETA refresh every 30 s during active delivery transit ─────
            var lastEtaRefresh = d.LastEtaRefreshAt;
            if (d.Phase == DeliveryPhase.MovingToDropoff &&
                d.ActiveDeliveryId.HasValue &&
                (now - lastEtaRefresh).TotalSeconds >= 30)
            {
                var remaining    = Math.Max(0, d.Waypoints.Count - nextIdx);
                var remainingSecs = remaining * opts.PositionUpdateIntervalMs / 1000.0 * opts.EtaBufferMultiplier;
                await SetDeliveryEtaAsync(d.ActiveDeliveryId.Value, now.AddSeconds(remainingSecs), ct);

                var etaPayload = new
                {
                    deliveryId           = d.ActiveDeliveryId.Value,
                    status               = "InTransit",
                    assignedDriverId     = (Guid?)d.Id,
                    etaSeconds           = remainingSecs > 0 ? (int?)((int)remainingSecs) : null,
                    routeDistanceMeters  = (double?)null,
                    routeDurationSeconds = (double?)null,
                    routeCost            = (decimal?)null,
                };
                var etaGroupIds = await districtGroupCache.GetGroupIdsForDistrictAsync(d.DistrictId, ct);
                foreach (var egid in etaGroupIds)
                    broadcastTasks.Add(hub.Clients.Group($"dg:{egid}").SendCoreAsync("DeliveryUpdated", [etaPayload], ct));

                lastEtaRefresh = now;
            }

            _drivers[i] = d with
            {
                WaypointIndex    = nextIdx,
                Direction        = nextDir,
                LastBroadcastAt  = now,
                LastEtaRefreshAt = lastEtaRefresh,
            };

            var routeAhead = d.Phase != DeliveryPhase.Patrol
                ? d.Waypoints.Skip(d.WaypointIndex)
                    .Select(static w => new[] { w.Lat, w.Lng })
                    .ToArray()
                : null;

            var posEntry = new
            {
                driverId   = d.Id,
                lat, lng,
                districtId = d.DistrictId,
                deliveryId = d.ActiveDeliveryId?.ToString(),
                routeAhead,
            };
            positionBatch.Add(posEntry);
            if (!batchByDistrict.TryGetValue(d.DistrictId, out var posBucket))
                batchByDistrict[d.DistrictId] = posBucket = [];
            posBucket.Add(posEntry);
        }

        // Fan-out per district group so each observer only receives their sector's positions.
        foreach (var (districtId, positions) in batchByDistrict)
        {
            var groupIds = await districtGroupCache.GetGroupIdsForDistrictAsync(districtId, ct);
            foreach (var gid in groupIds)
                broadcastTasks.Add(hub.Clients.Group($"dg:{gid}").SendCoreAsync("DriverPositionBatch", [positions], ct));
        }
        try { await Task.WhenAll(broadcastTasks); }
        catch (Exception ex) { logger.LogWarning(ex, "PositionSimulator: broadcast error"); }

        TickDuration.Record(sw.Elapsed.TotalMilliseconds);
        BatchSize.Record(positionBatch.Count);
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
                // Return forward path only. Patrol bouncing (A→B→A) is handled by AdvanceIndex
                // reversing direction at boundaries — no need to embed the reverse in waypoints.
                // Delivery routes must end at B (destination), not wrap back to A.
                return pts.ToList();
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
