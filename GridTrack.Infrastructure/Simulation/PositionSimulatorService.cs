using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Interfaces;
using GridTrack.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GridTrack.Infrastructure.Simulation;

/// <summary>Replays OSRM-routed waypoints (ping-pong) per driver; falls back to circular path; broadcasts DriverPositionUpdated.</summary>
public sealed class PositionSimulatorService(
    ISqlConnectionFactory sqlFactory,
    IHubContext<DashboardHub> hub,
    IOsrmService osrm,
    IOptions<SimulatorOptions> options,
    ILogger<PositionSimulatorService> logger) : BackgroundService
{
    private sealed record SimDriver(
        Guid Id,
        string Name,
        string ShortName,
        string DistrictId,
        bool IsActive,
        IReadOnlyList<(double Lat, double Lng)> Waypoints,
        int WaypointIndex,
        int Direction); // +1 forward, -1 backward

    // Destination offsets used to create unique A→B routes for each driver (~500 m).
    private static readonly (double DLat, double DLng)[] DestOffsets =
    [
        ( 0.005,  0.000),
        ( 0.000,  0.005),
        (-0.005,  0.000),
        ( 0.000, -0.005),
        ( 0.003,  0.004),
        (-0.004,  0.003),
        ( 0.004, -0.003),
        (-0.003, -0.004),
    ];

    private List<SimDriver> _drivers = [];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            logger.LogInformation("PositionSimulator disabled via Simulation:Enabled=false");
            return;
        }

        // Give the seeder time to finish before we read drivers.
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        await LoadDriversAsync(ct);

        if (_drivers.Count == 0)
        {
            logger.LogWarning("PositionSimulator: no active drivers found — simulation not started");
            return;
        }

        logger.LogInformation(
            "PositionSimulator started — {Count} drivers, interval {Ms} ms",
            _drivers.Count, opts.PositionUpdateIntervalMs);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(opts.PositionUpdateIntervalMs, ct);
            await TickAsync(ct);
        }
    }

    private async Task LoadDriversAsync(CancellationToken ct)
    {
        try
        {
            using var conn = sqlFactory.CreateConnection();

            const string sql = """
                SELECT "DriverId" AS Id, "Name", "ShortName", "DistrictId",
                       ST_Y("Location") AS CenterLat, ST_X("Location") AS CenterLng,
                       "IsActive"
                FROM public."Drivers"
                WHERE "IsActive" = true
                """;

            var rows = (await conn.QueryAsync<DriverRow>(sql)).ToList();

            var drivers = new List<SimDriver>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var waypoints = await FetchRouteWaypointsAsync(r, i, ct);
                // Spread starting positions so drivers aren't all at waypoint 0
                var startIndex = waypoints.Count > 1 ? i % waypoints.Count : 0;
                drivers.Add(new SimDriver(
                    r.Id, r.Name, r.ShortName, r.DistrictId, r.IsActive,
                    Waypoints: waypoints,
                    WaypointIndex: startIndex,
                    Direction: 1));
            }

            _drivers = drivers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PositionSimulator: failed to load drivers from DB");
        }
    }

    private async Task<IReadOnlyList<(double Lat, double Lng)>> FetchRouteWaypointsAsync(
        DriverRow driver, int index, CancellationToken ct)
    {
        var (dLat, dLng) = DestOffsets[index % DestOffsets.Length];

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var route = await osrm.GetRouteAsync(
                driver.CenterLat, driver.CenterLng,
                driver.CenterLat + dLat, driver.CenterLng + dLng,
                cts.Token);

            if (route?.Waypoints is { Count: > 1 } pts)
            {
                // Build ping-pong path: A→B then B→A (skip duplicate endpoint on reversal)
                var forward = pts.ToList();
                var reverse = forward.AsEnumerable().Reverse().Skip(1).ToList();
                var combined = new List<(double, double)>(forward.Count + reverse.Count);
                combined.AddRange(forward);
                combined.AddRange(reverse);
                logger.LogDebug(
                    "PositionSimulator: driver {Id} route has {N} waypoints",
                    driver.Id, combined.Count);
                return combined;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PositionSimulator: OSRM route fetch failed for driver {Id}, using circular fallback",
                driver.Id);
        }

        return BuildCircularFallback(driver.CenterLat, driver.CenterLng);
    }

    private static IReadOnlyList<(double Lat, double Lng)> BuildCircularFallback(
        double centerLat, double centerLng)
    {
        const double radius = 0.002;
        const int steps = 60;
        var pts = new List<(double, double)>(steps);
        for (var i = 0; i < steps; i++)
        {
            var angle = 2 * Math.PI * i / steps;
            pts.Add((centerLat + radius * Math.Sin(angle), centerLng + radius * Math.Cos(angle)));
        }
        return pts;
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var tasks = new List<Task>(_drivers.Count);

        for (var i = 0; i < _drivers.Count; i++)
        {
            var d = _drivers[i];
            if (d.Waypoints.Count == 0) continue;

            var (lat, lng) = d.Waypoints[d.WaypointIndex];

            // Advance index with ping-pong at boundaries
            var nextIndex = d.WaypointIndex + d.Direction;
            var nextDir = d.Direction;
            if (nextIndex >= d.Waypoints.Count) { nextIndex = d.Waypoints.Count - 2; nextDir = -1; }
            else if (nextIndex < 0) { nextIndex = 1; nextDir = 1; }

            _drivers[i] = d with { WaypointIndex = nextIndex, Direction = nextDir };

            tasks.Add(hub.Clients.All.SendCoreAsync(
                "DriverPositionUpdated",
                [new { driverId = d.Id, lat, lng, districtId = d.DistrictId }],
                ct));
        }

        try { await Task.WhenAll(tasks); }
        catch (Exception ex) { logger.LogWarning(ex, "PositionSimulator: broadcast error on tick"); }
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
}
