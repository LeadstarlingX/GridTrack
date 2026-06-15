using GridTrack.Application.Dtos;
using GridTrack.Domain.ValueObjects;
using GridTrack.Infrastructure.Hubs;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.UnitTests.Hubs;

public class DashboardPushServiceTests
{
    private static readonly GeometryFactory Factory = new();

    // ──────────────────────────────────────────────────────────────
    // BroadcastDriverPositionAsync
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task BroadcastDriverPosition_Should_Target_Correct_Group()
    {
        var (svc, hub) = Build();

        await svc.BroadcastDriverPositionAsync("mezzeh", BuildDriverDto("mezzeh"), CancellationToken.None);

        await Assert.That(hub.FakeClients.LastGroupName).IsEqualTo("mezzeh");
    }

    [Test]
    public async Task BroadcastDriverPosition_Should_Use_DriverPositionUpdated_Method_Name()
    {
        var (svc, hub) = Build();

        await svc.BroadcastDriverPositionAsync("mezzeh", BuildDriverDto("mezzeh"), CancellationToken.None);

        await Assert.That(hub.FakeClients.GroupProxy.Calls).Count().IsEqualTo(1);
        await Assert.That(hub.FakeClients.GroupProxy.Calls[0].Method).IsEqualTo("DriverPositionUpdated");
    }

    [Test]
    public async Task BroadcastDriverPosition_Should_Include_DriverId_In_Payload()
    {
        var (svc, hub) = Build();
        var driverId = Guid.NewGuid();
        var dto = new DriverDto
        {
            DriverId = driverId,
            Location = Factory.CreatePoint(new Coordinate(36.3, 33.5)),
            DistrictId = "mezzeh",
        };

        await svc.BroadcastDriverPositionAsync("mezzeh", dto, CancellationToken.None);

        var payload = hub.FakeClients.GroupProxy.Calls[0].Args[0]!;
        var actual = GetProperty<Guid>(payload, "driverId");
        await Assert.That(actual).IsEqualTo(driverId);
    }

    // ──────────────────────────────────────────────────────────────
    // BroadcastDeliveryUpdateAsync
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task BroadcastDeliveryUpdate_Should_Target_Correct_Group()
    {
        var (svc, hub) = Build();

        await svc.BroadcastDeliveryUpdateAsync("kafrsousa", BuildDeliveryDto(), CancellationToken.None);

        await Assert.That(hub.FakeClients.LastGroupName).IsEqualTo("kafrsousa");
    }

    [Test]
    public async Task BroadcastDeliveryUpdate_Should_Use_DeliveryUpdated_Method_Name()
    {
        var (svc, hub) = Build();

        await svc.BroadcastDeliveryUpdateAsync("kafrsousa", BuildDeliveryDto(), CancellationToken.None);

        await Assert.That(hub.FakeClients.GroupProxy.Calls[0].Method).IsEqualTo("DeliveryUpdated");
    }

    [Test]
    public async Task BroadcastDeliveryUpdate_Should_Include_DeliveryId_In_Payload()
    {
        var (svc, hub) = Build();
        var deliveryId = Guid.NewGuid();
        var dto = new DeliveryDto { DeliveryId = deliveryId, Status = DeliveryStatus.InTransit };

        await svc.BroadcastDeliveryUpdateAsync("malki", dto, CancellationToken.None);

        var payload = hub.FakeClients.GroupProxy.Calls[0].Args[0]!;
        await Assert.That(GetProperty<Guid>(payload, "deliveryId")).IsEqualTo(deliveryId);
    }

    // ──────────────────────────────────────────────────────────────
    // BroadcastAnomalyAsync
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task BroadcastAnomaly_Should_Target_Correct_Group()
    {
        var (svc, hub) = Build();

        await svc.BroadcastAnomalyAsync("babtouma", BuildAnomalyDto("babtouma"), CancellationToken.None);

        await Assert.That(hub.FakeClients.LastGroupName).IsEqualTo("babtouma");
    }

    [Test]
    public async Task BroadcastAnomaly_Should_Use_AnomalyBroadcast_Method_Name()
    {
        var (svc, hub) = Build();

        await svc.BroadcastAnomalyAsync("babtouma", BuildAnomalyDto("babtouma"), CancellationToken.None);

        await Assert.That(hub.FakeClients.GroupProxy.Calls[0].Method).IsEqualTo("AnomalyBroadcast");
    }

    [Test]
    public async Task BroadcastAnomaly_Should_Include_DeliveryId_And_Reason_In_Payload()
    {
        var (svc, hub) = Build();
        var deliveryId = Guid.NewGuid();
        var dto = new AnomalyAlertDto
        {
            DeliveryId = deliveryId,
            DistrictId = "mezzeh",
            Type = AnomalyType.StalePosition,
            Reason = "No movement for 30 min",
            Timestamp = DateTime.UtcNow,
        };

        await svc.BroadcastAnomalyAsync("mezzeh", dto, CancellationToken.None);

        var payload = hub.FakeClients.GroupProxy.Calls[0].Args[0]!;
        await Assert.That(GetProperty<Guid>(payload, "deliveryId")).IsEqualTo(deliveryId);
        await Assert.That(GetProperty<string>(payload, "reason")).IsEqualTo("No movement for 30 min");
    }

    // ──────────────────────────────────────────────────────────────
    // BroadcastForecastOverlayAsync
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task BroadcastForecastOverlay_Should_Target_Correct_Group()
    {
        var (svc, hub) = Build();
        var dto = new ForecastDto("malki", DateTime.UtcNow, 10, DateTime.UtcNow);

        await svc.BroadcastForecastOverlayAsync("malki", dto, CancellationToken.None);

        await Assert.That(hub.FakeClients.LastGroupName).IsEqualTo("malki");
    }

    [Test]
    public async Task BroadcastForecastOverlay_Should_Use_ForecastOverlayUpdated_Method_Name()
    {
        var (svc, hub) = Build();
        var dto = new ForecastDto("malki", DateTime.UtcNow, 10, DateTime.UtcNow);

        await svc.BroadcastForecastOverlayAsync("malki", dto, CancellationToken.None);

        await Assert.That(hub.FakeClients.GroupProxy.Calls[0].Method).IsEqualTo("ForecastOverlayUpdated");
    }

    // ──────────────────────────────────────────────────────────────
    // BroadcastUrgencyUpdateAsync
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task BroadcastUrgencyUpdate_Should_Target_All_Clients_When_DistrictId_Is_Null()
    {
        var (svc, hub) = Build();

        await svc.BroadcastUrgencyUpdateAsync(Guid.NewGuid(), null, 7, "ETA breach.", CancellationToken.None);

        await Assert.That(hub.FakeClients.AllProxy.Calls).Count().IsEqualTo(1);
        await Assert.That(hub.FakeClients.GroupProxy.Calls).Count().IsEqualTo(0);
    }

    [Test]
    public async Task BroadcastUrgencyUpdate_Should_Target_District_Group_When_DistrictId_Provided()
    {
        var (svc, hub) = Build();

        await svc.BroadcastUrgencyUpdateAsync(Guid.NewGuid(), "mezzeh", 7, "ETA breach.", CancellationToken.None);

        await Assert.That(hub.FakeClients.LastGroupName).IsEqualTo("mezzeh");
        await Assert.That(hub.FakeClients.GroupProxy.Calls).Count().IsEqualTo(1);
        await Assert.That(hub.FakeClients.AllProxy.Calls).Count().IsEqualTo(0);
    }

    [Test]
    public async Task BroadcastUrgencyUpdate_Should_Use_UrgencyUpdated_Method_Name()
    {
        var (svc, hub) = Build();

        await svc.BroadcastUrgencyUpdateAsync(Guid.NewGuid(), null, 9, "Critical.", CancellationToken.None);

        await Assert.That(hub.FakeClients.AllProxy.Calls[0].Method).IsEqualTo("UrgencyUpdated");
    }

    [Test]
    public async Task BroadcastUrgencyUpdate_Should_Include_Score_And_Note_In_Payload()
    {
        var (svc, hub) = Build();
        var deliveryId = Guid.NewGuid();

        await svc.BroadcastUrgencyUpdateAsync(deliveryId, null, 8, "Significant delay.", CancellationToken.None);

        var payload = hub.FakeClients.AllProxy.Calls[0].Args[0]!;
        await Assert.That(GetProperty<Guid>(payload, "deliveryId")).IsEqualTo(deliveryId);
        await Assert.That(GetProperty<int>(payload, "urgencyScore")).IsEqualTo(8);
        await Assert.That(GetProperty<string>(payload, "aiNote")).IsEqualTo("Significant delay.");
    }

    // ──────────────────────────────────────────────────────────────
    // BroadcastForecastResultAsync
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task BroadcastForecastResult_Should_Target_Correct_Group()
    {
        var (svc, hub) = Build();

        await svc.BroadcastForecastResultAsync(
            "kafrsousa", 12, DateTime.UtcNow, CancellationToken.None);

        await Assert.That(hub.FakeClients.LastGroupName).IsEqualTo("kafrsousa");
    }

    [Test]
    public async Task BroadcastForecastResult_Should_Use_ForecastOverlayUpdated_Method_Name()
    {
        var (svc, hub) = Build();

        await svc.BroadcastForecastResultAsync(
            "mezzeh", 8, DateTime.UtcNow, CancellationToken.None);

        await Assert.That(hub.FakeClients.GroupProxy.Calls[0].Method).IsEqualTo("ForecastOverlayUpdated");
    }

    [Test]
    public async Task BroadcastForecastResult_Should_Include_ForecastedDemand_And_UpdatedAt_In_Payload()
    {
        var (svc, hub) = Build();
        var updatedAt = new DateTime(2026, 6, 8, 14, 0, 0, DateTimeKind.Utc);

        await svc.BroadcastForecastResultAsync(
            "malki", 15, updatedAt, CancellationToken.None);

        var payload = hub.FakeClients.GroupProxy.Calls[0].Args[0]!;
        await Assert.That(GetProperty<int>(payload, "forecastedDemand")).IsEqualTo(15);
        await Assert.That(GetProperty<DateTime>(payload, "updatedAt")).IsEqualTo(updatedAt);
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static (DashboardPushService Service, FakeHubContext Hub) Build()
    {
        var hub = new FakeHubContext();
        return (new DashboardPushService(hub), hub);
    }

    /// <summary>Reads a named property from an anonymous object via reflection.</summary>
    private static T GetProperty<T>(object obj, string name)
    {
        var value = obj.GetType().GetProperty(name)!.GetValue(obj);
        return (T)value!;
    }

    private static DriverDto BuildDriverDto(string districtId) => new()
    {
        DriverId = Guid.NewGuid(),
        Location = Factory.CreatePoint(new Coordinate(36.3, 33.5)),
        DistrictId = districtId,
    };

    private static DeliveryDto BuildDeliveryDto() => new()
    {
        DeliveryId = Guid.NewGuid(),
        Status = DeliveryStatus.Assigned,
    };

    private static AnomalyAlertDto BuildAnomalyDto(string districtId) => new()
    {
        DeliveryId = Guid.NewGuid(),
        DistrictId = districtId,
        Type = AnomalyType.StalePosition,
        Reason = "stalled",
        Timestamp = DateTime.UtcNow,
    };
}
