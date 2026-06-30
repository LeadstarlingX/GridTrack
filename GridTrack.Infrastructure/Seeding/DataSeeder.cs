using System.Security.Cryptography;
using System.Text;
using GridTrack.Application.Dispatch;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Domain.H3Districts;
using GridTrack.Domain.ValueObjects;
using GridTrack.Infrastructure.Data;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.Seeding;

public sealed class DataSeeder(
    AppDbContext db,
    IOsrmService osrm,
    IDistrictDataService districtService,
    IRouteCostCalculator costCalculator,
    ILogger<DataSeeder> logger)
{
    // Max simultaneous OSRM requests during seeding. The 150 route calls used to run
    // strictly sequentially (~1 s each ≈ 2.5 min); fanning out drops that to ~15 s.
    private const int OsrmConcurrency = 12;

    private sealed record DeliverySpec(
        Driver Driver, Point Origin, Point Destination, DateTime CreatedAt, bool IsActive);

    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static readonly Random Rng = new(42);

    // District is assigned at runtime from GeoJSON neighborhoods (index % allDistricts.Count)
    private static readonly (string Name, string Short, CarType CarType, string LicensePlate, string Phone, decimal CapacityKg, string? Shift)[] DriverSeeds =
    [
        ("أحمد حسن",      "أحمد",     CarType.Sedan,      "10010001", "+963911100001", 200m,   "morning"),
        ("علي صالح",      "علي",      CarType.Sedan,      "10010002", "+963911100002", 200m,   "evening"),
        ("خالد بركات",    "خالد",     CarType.Sedan,      "10010003", "+963955100003", 200m,   "morning"),
        ("جهاد المير",    "جهاد",     CarType.Motorcycle, "10010004", "+963944100004", 30m,    "evening"),
        ("حسام حموية",    "حسام",     CarType.Van,        "10010005", "+963911100005", 1000m,  "morning"),
        ("ربيع حداد",     "ربيع",     CarType.Sedan,      "10010006", "+963955100006", 200m,   "evening"),
        ("منير قشاش",     "منير",     CarType.Sedan,      "10010007", "+963944100007", 200m,   "morning"),
        ("هشام برو",      "هشام",     CarType.Motorcycle, "10010008", "+963911100008", 30m,    "evening"),
        ("نزار مراد",     "نزار",     CarType.Sedan,      "10010009", "+963955100009", 200m,   "morning"),
        ("سهيل منصور",    "سهيل",     CarType.Van,        "10010010", "+963944100010", 1000m,  "evening"),
        ("حارث الحلبي",   "حارث",     CarType.Sedan,      "10010011", "+963911100011", 200m,   "morning"),
        ("زياد شامي",     "زياد",     CarType.Motorcycle, "10010012", "+963955100012", 30m,    null),   // INACTIVE
        ("سامي كريمي",    "سامي",     CarType.Motorcycle, "10010013", "+963944100013", 30m,    "evening"),
        ("طارق سليم",     "طارق",     CarType.Sedan,      "10010014", "+963911100014", 200m,   "morning"),
        ("كريم عوض",      "كريم",     CarType.Truck,      "10010015", "+963955100015", 5000m,  "evening"),
        ("أسامة طراد",    "أسامة",    CarType.Sedan,      "10010016", "+963944100016", 200m,   "morning"),
        ("عمار خليل",     "عمار",     CarType.Van,        "10010017", "+963911100017", 1000m,  "evening"),
        ("ياسر صفا",      "ياسر",     CarType.Sedan,      "10010018", "+963955100018", 200m,   "morning"),
        ("سعد الدين",     "سعد",      CarType.Motorcycle, "10010019", "+963944100019", 30m,    "evening"),
        ("فيصل راشد",     "فيصل",     CarType.Sedan,      "10010020", "+963911100020", 200m,   "morning"),
        ("رفيق صعب",      "رفيق",     CarType.Van,        "10010021", "+963955100021", 1000m,  "evening"),
        ("غسان سيروان",   "غسان",     CarType.Sedan,      "10010022", "+963944100022", 200m,   "morning"),
        ("عبد الله ديب",  "عبد الله", CarType.Truck,      "10010023", "+963911100023", 5000m,  "evening"),
        ("محمود سعيد",    "محمود",    CarType.Sedan,      "10010024", "+963955100024", 200m,   "morning"),
        ("باسل حمدان",    "باسل",     CarType.Motorcycle, "10010025", "+963944100025", 30m,    "evening"),
        ("ماهر طايح",     "ماهر",     CarType.Truck,      "10010026", "+963911100026", 5000m,  "morning"),
        ("رامي عباس",     "رامي",     CarType.Van,        "10010027", "+963955100027", 1000m,  "evening"),
        ("وائل داهر",     "وائل",     CarType.Sedan,      "10010028", "+963944100028", 200m,   "morning"),
        ("يوسف قاسم",     "يوسف",     CarType.Sedan,      "10010029", "+963911100029", 200m,   "evening"),
        ("مازن صليبا",    "مازن",     CarType.Motorcycle, "10010030", "+963955100030", 30m,    "morning"),
        ("سلمان بزي",     "سلمان",    CarType.Sedan,      "10010031", "+963944100031", 200m,   "evening"),
        ("بلال غانم",     "بلال",     CarType.Van,        "10010032", "+963911100032", 1000m,  "morning"),
        ("رضوان عرابي",   "رضوان",    CarType.Sedan,      "10010033", "+963955100033", 200m,   "evening"),
        ("أيمن قطريب",    "أيمن",     CarType.Truck,      "10010034", "+963944100034", 5000m,  "morning"),
        ("جورج خوري",     "جورج",     CarType.Sedan,      "10010035", "+963911100035", 200m,   "evening"),
        ("طلال سلوم",     "طلال",     CarType.Motorcycle, "10010036", "+963955100036", 30m,    "morning"),
        ("زهير طوبال",    "زهير",     CarType.Van,        "10010037", "+963944100037", 1000m,  "evening"),
        ("أنس قطيني",     "أنس",      CarType.Sedan,      "10010038", "+963911100038", 200m,   "morning"),
        ("عمر رحال",      "عمر",      CarType.Van,        "10010039", "+963955100039", 1000m,  "morning"),
        ("فادي جبري",     "فادي",     CarType.Motorcycle, "10010040", "+963944100040", 30m,    "evening"),
        ("حسن نصار",      "حسن",      CarType.Sedan,      "10010041", "+963911100041", 200m,   "morning"),
        ("إبراهيم لحام",  "إبراهيم",  CarType.Truck,      "10010042", "+963955100042", 5000m,  "evening"),
        ("ناصر عيسى",     "ناصر",     CarType.Sedan,      "10010043", "+963944100043", 200m,   "morning"),
        ("نضال شهاب",     "نضال",     CarType.Motorcycle, "10010044", "+963911100044", 30m,    "evening"),
        ("لؤي فتال",      "لؤي",      CarType.Sedan,      "10010045", "+963955100045", 200m,   "morning"),
        ("جمال عويس",     "جمال",     CarType.Van,        "10010046", "+963944100046", 1000m,  "evening"),
        ("توفيق نيال",    "توفيق",    CarType.Sedan,      "10010047", "+963911100047", 200m,   "morning"),
        ("عدنان جوهر",    "عدنان",    CarType.Truck,      "10010048", "+963955100048", 5000m,  "evening"),
        ("مصطفى حلبي",    "مصطفى",    CarType.Sedan,      "10010049", "+963944100049", 200m,   "morning"),
        ("تامر ناصيف",    "تامر",     CarType.Motorcycle, "10010050", "+963911100050", 30m,    "evening"),
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        var allDistricts = districtService.GetAll();
        if (allDistricts.Count == 0)
        {
            logger.LogError("DataSeeder: no districts loaded from GeoJSON — aborting seed");
            return;
        }

        var now = DateTime.UtcNow;

        // ── District boundaries (H3District) ──────────────────────────────
        // Powers GET /api/districts/boundaries + the frontend district polygon layer.
        // Idempotent: SeedService doesn't clear this table on reseed, so we only seed when empty.
        await SeedBoundariesAsync(allDistricts, ct);

        // ── Drivers ───────────────────────────────────────────────────────
        var today = now.Date;
        var drivers = new List<Driver>();
        for (var i = 0; i < DriverSeeds.Length; i++)
        {
            var seed = DriverSeeds[i];
            var driverId = DeterministicGuid(seed.LicensePlate);
            var district = allDistricts[i % allDistricts.Count];
            var location = Jitter(district.CentroidLat, district.CentroidLng, district.JitterRadius);
            var isActive = seed.LicensePlate != "10010012"; // زياد شامي is offline

            DateTime? shiftStart = seed.Shift switch
            {
                "morning" => today.AddHours(6),
                "evening" => today.AddHours(14),
                _         => null,
            };
            DateTime? shiftEnd = seed.Shift switch
            {
                "morning" => today.AddHours(14),
                "evening" => today.AddHours(22),
                _         => null,
            };

            var result = Driver.Create(driverId, location, district.Id, now, seed.Name, seed.Short, isActive,
                seed.CarType, seed.LicensePlate, seed.Phone, seed.CapacityKg, shiftStart, shiftEnd);
            if (result.IsFailure)
            {
                logger.LogWarning("Failed to create driver {Name}: {Error}", seed.Name, result.Error.Message);
                continue;
            }

            result.Value.ClearDomainEvents();
            drivers.Add(result.Value);
        }

        db.Set<Driver>().AddRange(drivers);
        await db.SaveChangesAsync(ct);

        // ── Build delivery specs (no network yet) ─────────────────────────
        // ~40 are in-progress *right now*, each on a distinct driver, so the dashboard
        // shows many drivers with active orders. ~60 land in the last ~11h specifically —
        // a dense band so the pickup-density heatmap (1-12h lookback slider) always has
        // something to show, regardless of where the slider sits. The rest are spread across
        // the full 7 days for weekly trend views.
        var activeDrivers = drivers.Where(d => d.IsActive).ToList();
        const int activeCount = 40;
        const int recentCount = 60;
        const int historicalCount = 110;

        var specs = new List<DeliverySpec>(activeCount + recentCount + historicalCount);
        for (var i = 0; i < activeCount + recentCount + historicalCount; i++)
        {
            var isActive = i < activeCount;
            var isRecent = !isActive && i < activeCount + recentCount;
            var driver = activeDrivers[i % activeDrivers.Count];

            var originDistrict = districtService.GetById(driver.DistrictId);
            if (originDistrict is null) continue;
            var origin = Jitter(originDistrict.CentroidLat, originDistrict.CentroidLng, originDistrict.JitterRadius);

            var destDistrict = districtService.GetRandom(driver.DistrictId);
            var destination = Jitter(destDistrict.CentroidLat, destDistrict.CentroidLng, destDistrict.JitterRadius);

            var createdAt = isActive
                ? now.AddMinutes(-Rng.Next(15, 90))  // recent → still on the road
                : isRecent
                    ? now.AddMinutes(-Rng.Next(60, 11 * 60))  // last ~11h, leaves room to complete within 12h
                    : now.AddDays(-7).AddSeconds(Rng.Next(0, (int)TimeSpan.FromDays(7).TotalSeconds));

            specs.Add(new DeliverySpec(driver, origin, destination, createdAt, isActive));
        }

        // ── Fetch all OSRM routes in parallel (bounded) ───────────────────
        var routes = await FetchRoutesAsync(specs, ct);

        // ── Materialize delivery aggregates ───────────────────────────────
        var deliveryRoutes = new List<DeliveryRoute>();
        for (var i = 0; i < specs.Count; i++)
        {
            var spec  = specs[i];
            var route = routes[i];

            var durationSeconds = route?.DurationSeconds ?? RandomRange(900, 3600);
            var distanceMeters  = route?.DistanceMeters  ?? durationSeconds * 8.0; // ~8 m/s fallback
            var waypoints       = route?.Waypoints ?? [];

            var expectedEta = spec.CreatedAt.AddSeconds(durationSeconds);
            var deliveryId  = Guid.NewGuid();

            var deliveryResult = Delivery.Create(deliveryId, spec.Origin, spec.Driver.DistrictId, spec.CreatedAt, expectedEta);
            if (deliveryResult.IsFailure) continue;

            var delivery = deliveryResult.Value;
            delivery.ClearDomainEvents();

            var assignedAt = spec.CreatedAt.AddMinutes(RandomRange(2, 5));
            var pickedUpAt = assignedAt.AddMinutes(RandomRange(5, 10));

            delivery.AssignDriver(spec.Driver.DriverId);
            delivery.MarkPickedUp(spec.Origin, pickedUpAt);
            delivery.UpdateLocation(spec.Origin, pickedUpAt); // → InTransit
            delivery.SetRoute(distanceMeters, durationSeconds,
                costCalculator.Calculate(distanceMeters, durationSeconds));
            delivery.ClearDomainEvents();

            if (!spec.IsActive)
            {
                var factor = 0.8 + Rng.NextDouble() * 0.4;
                var deliveredAt = pickedUpAt.AddSeconds(durationSeconds * factor);

                var isAnomalous = i % 100 < 12;
                if (isAnomalous)
                {
                    var anomalyType = (i % 3) switch
                    {
                        0 => AnomalyType.UnexpectedStop,
                        1 => AnomalyType.EtaExceeded,
                        _ => AnomalyType.RouteDeviation,
                    };
                    var reason = anomalyType switch
                    {
                        AnomalyType.UnexpectedStop => $"No movement for {Rng.Next(10, 30)} min",
                        AnomalyType.EtaExceeded    => $"ETA exceeded by {Rng.Next(10, 30)} min",
                        _                          => "Driver deviated from expected route",
                    };
                    delivery.FlagAnomaly(anomalyType, reason);
                }
                else
                {
                    delivery.MarkDelivered(deliveredAt);
                }

                delivery.ClearDomainEvents();
            }

            db.Set<Delivery>().Add(delivery);

            for (var seq = 0; seq < waypoints.Count; seq++)
            {
                deliveryRoutes.Add(new DeliveryRoute
                {
                    DeliveryId = deliveryId,
                    Sequence   = seq,
                    Lat        = waypoints[seq].Lat,
                    Lng        = waypoints[seq].Lng,
                });
            }
        }

        db.Set<DeliveryRoute>().AddRange(deliveryRoutes);
        await db.SaveChangesAsync(ct);

        // ── Pending deliveries for the simulator ──────────────────────────
        var pendingDeliveries = new List<Delivery>();
        for (var i = 0; i < 80; i++)
        {
            var district = allDistricts[i % allDistricts.Count];
            var origin = Jitter(district.CentroidLat, district.CentroidLng, district.JitterRadius);
            var deliveryId = Guid.NewGuid();
            var createdAt = now.AddMinutes(-Rng.Next(1, 10));
            var eta = createdAt.AddMinutes(Rng.Next(20, 60));
            var result = Delivery.Create(deliveryId, origin, district.Id, createdAt, eta);
            if (result.IsFailure) continue;
            result.Value.ClearDomainEvents();
            pendingDeliveries.Add(result.Value);
        }
        db.Set<Delivery>().AddRange(pendingDeliveries);
        await db.SaveChangesAsync(ct);
    }

    // Fans OSRM calls out across OsrmConcurrency workers; preserves spec order in the
    // result array. A failed/absent route comes back as null and the caller falls back.
    private async Task<OsrmRouteResult?[]> FetchRoutesAsync(
        IReadOnlyList<DeliverySpec> specs, CancellationToken ct)
    {
        var results = new OsrmRouteResult?[specs.Count];
        using var gate = new SemaphoreSlim(OsrmConcurrency);

        var tasks = specs.Select(async (spec, i) =>
        {
            await gate.WaitAsync(ct);
            try
            {
                results[i] = await osrm.GetRouteAsync(
                    spec.Origin.Y, spec.Origin.X, spec.Destination.Y, spec.Destination.X, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OSRM call failed for delivery {Index}, using fallback", i);
                results[i] = null;
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    // Builds H3District rows from the GeoJSON neighborhood polygons. Keyed by the district's
    // osm_id (same id Drivers/Deliveries use as DistrictId), so the frontend can match polygons
    // to live data. Skipped entirely if the table already has rows.
    private async Task SeedBoundariesAsync(IReadOnlyList<DistrictInfo> districts, CancellationToken ct)
    {
        if (await db.Set<H3District>().AnyAsync(ct)) return;

        var boundaries = new List<H3District>();
        foreach (var d in districts)
        {
            if (d.Boundary is not { Count: >= 3 }) continue;

            var shell = d.Boundary.Select(p => new Coordinate(p[0], p[1])).ToList();
            if (!shell[0].Equals2D(shell[^1])) shell.Add(shell[0].Copy()); // a LinearRing must be closed
            if (shell.Count < 4) continue;

            var polygon = Geo.CreatePolygon(Geo.CreateLinearRing(shell.ToArray()));
            var center  = Geo.CreatePoint(new Coordinate(d.CentroidLng, d.CentroidLat));

            var result = H3District.Create(d.Id, center, polygon, resolution: 7);
            if (result.IsFailure)
            {
                logger.LogWarning("Failed to create H3District {Id}: {Error}", d.Id, result.Error.Message);
                continue;
            }

            result.Value.ClearDomainEvents();
            boundaries.Add(result.Value);
        }

        db.Set<H3District>().AddRange(boundaries);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} district boundaries (H3District)", boundaries.Count);
    }

    private static Guid DeterministicGuid(string seed)
        => new(MD5.HashData(Encoding.UTF8.GetBytes(seed)));

    private static Point Jitter(double lat, double lng, double radius)
    {
        var dlat = (Rng.NextDouble() * 2 - 1) * radius;
        var dlng = (Rng.NextDouble() * 2 - 1) * radius;
        return Geo.CreatePoint(new Coordinate(lng + dlng, lat + dlat));
    }

    private static double RandomRange(double min, double max)
        => min + Rng.NextDouble() * (max - min);
}
