using System.Security.Cryptography;
using System.Text;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Domain.ValueObjects;
using GridTrack.Infrastructure.Data;
using GridTrack.Infrastructure.DbContext;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.Seeding;

public sealed class DataSeeder(
    AppDbContext db,
    IOsrmService osrm,
    ILogger<DataSeeder> logger)
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static readonly Random Rng = new(42);

    // Districts: id → (centerLat, centerLng, jitterRadius)
    private static readonly (string Id, string Name, double Lat, double Lng, double Jitter)[] Districts =
    [
        ("mezzeh",    "Mezzeh",    33.505, 36.243, 0.008),
        ("kafrsousa", "Kafr Sousa", 33.497, 36.272, 0.007),
        ("malki",     "Malki",     33.517, 36.281, 0.006),
        ("babtouma",  "Bab Touma", 33.522, 36.307, 0.005),
    ];

    // Shift: "morning" = 06:00–14:00 UTC, "evening" = 14:00–22:00 UTC, null = no shift
    private static readonly (string Name, string Short, string District, CarType CarType, string LicensePlate, string Phone, decimal CapacityKg, string? Shift)[] DriverSeeds =
    [
        ("Ahmad Hassan",   "Ahmad",   "mezzeh",    CarType.Sedan,      "AHM-1001", "+963-911-101001", 200m,   "morning"),
        ("Sami Karimi",    "Sami",    "malki",     CarType.Motorcycle, "SKR-2002", "+963-944-202002", 30m,    "evening"),
        ("Omar Rahhal",    "Omar",    "babtouma",  CarType.Van,        "OMR-3003", "+963-955-303003", 1000m,  "morning"),
        ("Ali Saleh",      "Ali",     "mezzeh",    CarType.Sedan,      "ALS-4004", "+963-911-404004", 200m,   "evening"),
        ("Maher Tayeh",    "Maher",   "kafrsousa", CarType.Truck,      "MHT-5005", "+963-944-505005", 5000m,  "morning"),
        ("Khaled Barakat", "Khaled",  "mezzeh",    CarType.Sedan,      "KHB-6006", "+963-955-606006", 200m,   "evening"),
        ("Fadi Jabri",     "Fadi",    "babtouma",  CarType.Motorcycle, "FDJ-7007", "+963-911-707007", 30m,    "morning"),
        ("Rami Abbas",     "Rami",    "kafrsousa", CarType.Van,        "RMA-8008", "+963-944-808008", 1000m,  "evening"),
        ("Hassan Nassar",  "Hassan",  "babtouma",  CarType.Sedan,      "HSN-9009", "+963-955-909009", 200m,   "morning"),
        ("Wael Daher",     "Wael",    "kafrsousa", CarType.Sedan,      "WAD-1010", "+963-911-100010", 200m,   "evening"),
        ("Ibrahim Lahham", "Ibrahim", "babtouma",  CarType.Truck,      "IBL-1111", "+963-944-111011", 5000m,  "morning"),
        ("Ziad Chami",     "Ziad",    "mezzeh",    CarType.Motorcycle, "ZCH-1212", "+963-955-121212", 30m,    null),
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // ── Drivers ───────────────────────────────────────────────────────
        var today = now.Date;
        var drivers = new List<Driver>();
        foreach (var seed in DriverSeeds)
        {
            var driverId = DeterministicGuid(seed.Name);
            var district = Districts.First(d => d.Id == seed.District);
            var location = Jitter(district.Lat, district.Lng, district.Jitter);
            var isActive = seed.Name != "Ziad Chami"; // Ziad is offline

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

            var result = Driver.Create(driverId, location, seed.District, now, seed.Name, seed.Short, isActive,
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

        // ── Deliveries ────────────────────────────────────────────────────
        var activeDrivers = drivers.Where(d => d.IsActive).ToList();
        var deliveryRoutes = new List<DeliveryRoute>();

        for (var i = 0; i < 60; i++)
        {
            var driver = activeDrivers[i % activeDrivers.Count];
            var originDistrict = Districts.First(d => d.Id == driver.DistrictId);
            var origin = Jitter(originDistrict.Lat, originDistrict.Lng, originDistrict.Jitter);

            // Pick a different district for the destination
            var destDistrict = Districts
                .Where(d => d.Id != driver.DistrictId)
                .OrderBy(_ => Rng.Next())
                .First();
            var destination = Jitter(destDistrict.Lat, destDistrict.Lng, destDistrict.Jitter);

            // Spread createdAt over the last 7 days
            var createdAt = now.AddDays(-7).AddSeconds(Rng.Next(0, (int)TimeSpan.FromDays(7).TotalSeconds));
            var isPast = createdAt < now.AddHours(-2);

            // Call OSRM for route
            double durationSeconds = RandomRange(900, 3600); // fallback 15–60 min
            List<(double, double)> waypoints = [];
            try
            {
                var route = await osrm.GetRouteAsync(origin.Y, origin.X, destination.Y, destination.X, ct);
                if (route is not null)
                {
                    durationSeconds = route.DurationSeconds;
                    waypoints = route.Waypoints.ToList();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OSRM call failed for delivery {Index}, using fallback", i);
            }

            var expectedEta = createdAt.AddSeconds(durationSeconds);
            var deliveryId = Guid.NewGuid();

            var deliveryResult = Delivery.Create(deliveryId, origin, driver.DistrictId, createdAt, expectedEta);
            if (deliveryResult.IsFailure) continue;

            var delivery = deliveryResult.Value;
            delivery.ClearDomainEvents();

            // Advance state machine
            var assignedAt = createdAt.AddMinutes(RandomRange(2, 5));
            var pickedUpAt = assignedAt.AddMinutes(RandomRange(5, 10));

            delivery.AssignDriver(driver.DriverId);
            delivery.MarkPickedUp(origin, pickedUpAt);
            delivery.UpdateLocation(origin, pickedUpAt);
            delivery.ClearDomainEvents();

            if (isPast)
            {
                var factor = 0.8 + Rng.NextDouble() * 0.4; // 0.8–1.2
                var deliveredAt = pickedUpAt.AddSeconds(durationSeconds * factor);

                var isAnomalous = i % 100 < 12; // ~12%
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
                    // Anomalous deliveries stay flagged — no MarkDelivered
                }
                else
                {
                    delivery.MarkDelivered(deliveredAt);
                }

                delivery.ClearDomainEvents();
            }

            db.Set<Delivery>().Add(delivery);

            // Store waypoints
            for (var seq = 0; seq < waypoints.Count; seq++)
            {
                deliveryRoutes.Add(new DeliveryRoute
                {
                    DeliveryId = deliveryId,
                    Sequence   = seq,
                    Lat        = waypoints[seq].Item1,
                    Lng        = waypoints[seq].Item2,
                });
            }
        }

        db.Set<DeliveryRoute>().AddRange(deliveryRoutes);
        await db.SaveChangesAsync(ct);

        // ── Pending (Created) deliveries for the simulator to pick up ─────
        var pendingDeliveries = new List<Delivery>();
        for (var i = 0; i < 30; i++)
        {
            var district = Districts[i % Districts.Length];
            var origin = Jitter(district.Lat, district.Lng, district.Jitter);
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
