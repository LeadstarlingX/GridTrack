using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GridTrack.Infrastructure.Simulation;

/// <summary>
/// Simulates driver positions by moving each active driver in a circular patrol
/// around their seeded district center. Broadcasts DriverPositionUpdated to all
/// connected SignalR clients. Driven by Simulation:PositionUpdateIntervalMs.
/// </summary>
public sealed class PositionSimulatorService(
    ISqlConnectionFactory sqlFactory,
    IHubContext<DashboardHub> hub,
    IOptions<SimulatorOptions> options,
    ILogger<PositionSimulatorService> logger) : BackgroundService
{
    private sealed record SimDriver(
        Guid Id,
        string Name,
        string ShortName,
        string DistrictId,
        double CenterLat,
        double CenterLng,
        double Angle,
        bool IsActive);

    // Drivers keep a circle of this radius (degrees). ~0.002° ≈ 220 m.
    private const double RadiusDeg = 0.002;

    // Each driver completes one full circle every CirclePeriodMs milliseconds.
    private const double CirclePeriodMs = 120_000; // 2 minutes

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
            await TickAsync(opts.PositionUpdateIntervalMs, ct);
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

            var rows = await conn.QueryAsync<DriverRow>(sql);
            _drivers = rows.Select((r, i) => new SimDriver(
                r.Id, r.Name, r.ShortName, r.DistrictId,
                r.CenterLat, r.CenterLng,
                // Spread starting angles so drivers don't all move in sync
                Angle: 2 * Math.PI * i / Math.Max(1, rows.Count()),
                r.IsActive)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PositionSimulator: failed to load drivers from DB");
        }
    }

    private async Task TickAsync(int intervalMs, CancellationToken ct)
    {
        // How many radians to advance per tick to complete one circle in CirclePeriodMs
        var angleStep = 2 * Math.PI * intervalMs / CirclePeriodMs;

        var tasks = new List<Task>(_drivers.Count);

        for (var i = 0; i < _drivers.Count; i++)
        {
            var d = _drivers[i];
            var newAngle = d.Angle + angleStep;
            var lat = d.CenterLat + RadiusDeg * Math.Sin(newAngle);
            var lng = d.CenterLng + RadiusDeg * Math.Cos(newAngle);
            _drivers[i] = d with { Angle = newAngle };

            tasks.Add(hub.Clients.All.SendCoreAsync(
                "DriverPositionUpdated",
                [new
                {
                    driverId   = d.Id,
                    lat,
                    lng,
                    districtId = d.DistrictId,
                }],
                ct));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PositionSimulator: broadcast error on tick");
        }
    }

    // Dapper projection row
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
