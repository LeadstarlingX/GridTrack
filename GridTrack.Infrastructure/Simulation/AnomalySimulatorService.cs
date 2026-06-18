using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GridTrack.Infrastructure.Simulation;

public sealed class AnomalySimulatorService(
    ISqlConnectionFactory sqlFactory,
    IHubContext<DashboardHub> hub,
    IOptions<SimulatorOptions> options,
    ILogger<AnomalySimulatorService> logger) : BackgroundService
{
    private static readonly string[] IncidentSummaries =
    [
        "Cluster of stale positions detected in residential zone",
        "Multiple ETA breaches on main corridor — possible congestion",
        "Route deviations spiking near checkpoint area",
        "Unexpected stops following major delivery wave",
        "AI flagged anomaly cluster — review dispatch queue",
        "Driver grouping detected, possible road blockage",
        "Unusual stop pattern in commercial block",
        "Surge of unexpected-stop events — checkpoint suspected",
        "High anomaly density in district — auto-escalated",
        "ETA cascade breach — likely arterial road congestion",
    ];

    private static readonly string[] AnomalyReasons =
    [
        "No movement for over 12 minutes",
        "Driver left assigned delivery corridor",
        "ETA exceeded by 22 minutes due to congestion",
        "Vehicle stopped in non-delivery zone for 9 minutes",
        "Stale GPS — no position update for 18 minutes",
        "Route deviation detected near checkpoint",
        "Unexpected stop in residential area",
        "ETA missed — peak-hour traffic flagged by AI",
        "Driver stopped at unscheduled location",
        "Multiple route deviations within 5-minute window",
    ];

    private static readonly string[] AnomalyTypes = ["StalePosition", "RouteDeviation", "EtaExceeded", "UnexpectedStop"];

    private static readonly int[] WindowMinutes = [5, 10, 15, 30];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = options.Value;
        if (!opts.Enabled) { logger.LogInformation("AnomalySimulator disabled"); return; }

        // Stagger start so position simulator loads drivers first
        await Task.Delay(TimeSpan.FromSeconds(45), ct);
        logger.LogInformation("AnomalySimulator started — anomaly {A}ms / surge {S}ms / incident {I}ms",
            opts.AnomalyIntervalMs, opts.SurgeIntervalMs, opts.IncidentIntervalMs);

        await Task.WhenAll(
            RunLoopAsync(opts.AnomalyIntervalMs,  EmitAnomalyAsync,  ct),
            RunLoopAsync(opts.SurgeIntervalMs,     EmitSurgeAsync,    ct),
            RunLoopAsync(opts.IncidentIntervalMs,  EmitIncidentAsync, ct));
    }

    private static async Task RunLoopAsync(int intervalMs, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(intervalMs, ct);
            try { await action(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Swallow — simulator must never crash the host
            }
        }
    }

    private async Task EmitAnomalyAsync(CancellationToken ct)
    {
        ActiveDriver? driver;
        try
        {
            using var conn = sqlFactory.CreateConnection();
            driver = await conn.QueryFirstOrDefaultAsync<ActiveDriver>("""
                SELECT d."DriverId"   AS Id,
                       d."Name",
                       d."DistrictId",
                       ST_Y(d."Location") AS Lat,
                       ST_X(d."Location") AS Lng
                FROM public."Drivers" d
                WHERE d."IsActive" = true
                ORDER BY RANDOM()
                LIMIT 1
                """);
        }
        catch (Exception ex) { logger.LogWarning(ex, "AnomalySimulator: anomaly DB query failed"); return; }

        if (driver is null) return;

        await hub.Clients.All.SendCoreAsync("AnomalyBroadcast",
            [new
            {
                id          = $"sim-{Guid.NewGuid():N}",
                deliveryId  = Guid.NewGuid().ToString(),
                driverId    = driver.Id.ToString(),
                driverName  = driver.Name,
                anomalyType = Pick(AnomalyTypes),
                reason      = Pick(AnomalyReasons),
                districtId  = driver.DistrictId,
                lat         = driver.Lat + (Random.Shared.NextDouble() - 0.5) * 0.002,
                lng         = driver.Lng + (Random.Shared.NextDouble() - 0.5) * 0.002,
                timestamp   = DateTime.UtcNow,
            }],
            ct);
    }

    private async Task EmitSurgeAsync(CancellationToken ct)
    {
        string? districtId;
        try
        {
            using var conn = sqlFactory.CreateConnection();
            districtId = await conn.ExecuteScalarAsync<string?>("""
                SELECT "DistrictId"
                FROM public."Drivers"
                WHERE "IsActive" = true
                ORDER BY RANDOM()
                LIMIT 1
                """);
        }
        catch (Exception ex) { logger.LogWarning(ex, "AnomalySimulator: surge DB query failed"); return; }

        if (districtId is null) return;

        var mean       = 8.0 + Random.Shared.Next(8);
        var current    = (int)(mean + Random.Shared.Next(6, 16));
        var deviations = Math.Round((current - mean) / Math.Max(1, mean * 0.22), 2);

        await hub.Clients.All.SendCoreAsync("DemandSurge",
            [new
            {
                districtId,
                currentCount   = current,
                historicalMean = mean,
                deviations,
                detectedAt     = DateTime.UtcNow,
            }],
            ct);
    }

    private async Task EmitIncidentAsync(CancellationToken ct)
    {
        string? districtId;
        try
        {
            using var conn = sqlFactory.CreateConnection();
            districtId = await conn.ExecuteScalarAsync<string?>("""
                SELECT "DistrictId"
                FROM public."Drivers"
                WHERE "IsActive" = true
                ORDER BY RANDOM()
                LIMIT 1
                """);
        }
        catch (Exception ex) { logger.LogWarning(ex, "AnomalySimulator: incident DB query failed"); return; }

        if (districtId is null) return;

        await hub.Clients.All.SendCoreAsync("AnomalyIncident",
            [new
            {
                districtId,
                anomalyCount  = 3 + Random.Shared.Next(6),
                windowMinutes = Pick(WindowMinutes),
                summary       = Pick(IncidentSummaries),
                detectedAt    = DateTime.UtcNow,
            }],
            ct);
    }

    private static T Pick<T>(T[] array) => array[Random.Shared.Next(array.Length)];

    private sealed class ActiveDriver
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DistrictId { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
