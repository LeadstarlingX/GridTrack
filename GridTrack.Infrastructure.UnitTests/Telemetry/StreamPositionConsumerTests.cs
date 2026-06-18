using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Infrastructure.Telemetry;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.UnitTests.Telemetry;

public class StreamPositionConsumerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task BroadcastAsync_Should_Call_Push_With_Correct_DistrictId()
    {
        var push = new FakePush();
        var driverId = Guid.NewGuid();

        await StreamPositionConsumer.BroadcastAsync(
            driverId, lat: 33.5, lng: 36.3, "mezzeh",
            "Ahmad Hassan", "Ahmad", isActive: true, DateTime.UtcNow,
            push, CancellationToken.None);

        await Assert.That(push.Calls).Count().IsEqualTo(1);
        await Assert.That(push.Calls[0].DistrictId).IsEqualTo("mezzeh");
    }

    [Test]
    public async Task BroadcastAsync_Should_Map_DriverId_Correctly()
    {
        var push = new FakePush();
        var driverId = Guid.NewGuid();

        await StreamPositionConsumer.BroadcastAsync(
            driverId, lat: 33.5, lng: 36.3, "malki",
            "Sami Karimi", "Sami", isActive: false, DateTime.UtcNow,
            push, CancellationToken.None);

        await Assert.That(push.Calls[0].Dto.DriverId).IsEqualTo(driverId);
    }

    [Test]
    public async Task BroadcastAsync_Should_Map_Lat_Lng_Into_NTS_Point()
    {
        var push = new FakePush();

        await StreamPositionConsumer.BroadcastAsync(
            Guid.NewGuid(), lat: 33.51, lng: 36.27, "kafrsousa",
            "Omar", "O", isActive: true, DateTime.UtcNow,
            push, CancellationToken.None);

        var location = (Point)push.Calls[0].Dto.Location;
        // NTS Point: X = lng, Y = lat
        await Assert.That(location.Y).IsEqualTo(33.51);
        await Assert.That(location.X).IsEqualTo(36.27);
    }

    [Test]
    public async Task BroadcastAsync_Should_Map_IsActive_And_Timestamp()
    {
        var push = new FakePush();
        var ts = new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc);

        await StreamPositionConsumer.BroadcastAsync(
            Guid.NewGuid(), lat: 33.5, lng: 36.3, "babtouma",
            "Youssef", "Y", isActive: false, ts,
            push, CancellationToken.None);

        var dto = push.Calls[0].Dto;
        await Assert.That(dto.IsActive).IsFalse();
        await Assert.That(dto.LastSeen).IsEqualTo(ts);
    }

    [Test]
    public async Task BroadcastAsync_Should_Map_Name_And_ShortName()
    {
        var push = new FakePush();

        await StreamPositionConsumer.BroadcastAsync(
            Guid.NewGuid(), lat: 33.5, lng: 36.3, "mezzeh",
            "Ahmad Hassan", "Ahmad", isActive: true, DateTime.UtcNow,
            push, CancellationToken.None);

        var dto = push.Calls[0].Dto;
        await Assert.That(dto.Name).IsEqualTo("Ahmad Hassan");
        await Assert.That(dto.ShortName).IsEqualTo("Ahmad");
    }

    // ── Fake ─────────────────────────────────────────────────────────────────

    private sealed class FakePush : IDashboardPushService
    {
        public List<(string DistrictId, DriverDto Dto)> Calls { get; } = [];

        public Task BroadcastDriverPositionAsync(string districtId, DriverDto payload, CancellationToken ct)
        {
            Calls.Add((districtId, payload));
            return Task.CompletedTask;
        }

        public Task BroadcastDeliveryUpdateAsync(string d, DeliveryDto p, CancellationToken ct)       => Task.CompletedTask;
        public Task BroadcastAnomalyAsync(string d, AnomalyAlertDto p, CancellationToken ct)          => Task.CompletedTask;
        public Task BroadcastForecastOverlayAsync(string d, ForecastDto p, CancellationToken ct)      => Task.CompletedTask;
        public Task BroadcastUrgencyUpdateAsync(Guid id, string? d, int s, string n, CancellationToken ct) => Task.CompletedTask;
        public Task BroadcastForecastResultAsync(string d, int fd, DateTime u, CancellationToken ct)  => Task.CompletedTask;
        public Task BroadcastDemandSurgeAsync(string d, int c, double m, double dev, CancellationToken ct) => Task.CompletedTask;
        public Task BroadcastAnomalyIncidentAsync(string d, int count, string s, CancellationToken ct) => Task.CompletedTask;
    }
}
