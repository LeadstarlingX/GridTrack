using GridTrack.Application.CQRS.Handlers;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

public class IntegrationEventPublishersTests
{
    private static readonly GeometryFactory Factory = new();

    // ──────────────────────────────────────────────────────────────
    // AnomalyIntegrationPublisher
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Anomaly_Should_Map_DeliveryId_And_DistrictId()
    {
        var deliveryId = Guid.NewGuid();
        var e = new DeliveryFlaggedAnomalousDomainEvent(
            deliveryId, AnomalyType.StalePosition, "No movement", "mezzeh");

        var result = AnomalyIntegrationPublisher.Handle(e);

        await Assert.That(result.DeliveryId).IsEqualTo(deliveryId);
        await Assert.That(result.DistrictId).IsEqualTo("mezzeh");
    }

    [Test]
    public async Task Anomaly_Should_Map_AnomalyType_As_String()
    {
        var e = new DeliveryFlaggedAnomalousDomainEvent(
            Guid.NewGuid(), AnomalyType.RouteDeviation, "Left route", "kafrsousa");

        var result = AnomalyIntegrationPublisher.Handle(e);

        await Assert.That(result.AnomalyType).IsEqualTo("RouteDeviation");
    }

    [Test]
    public async Task Anomaly_Should_Carry_Reason_In_Output()
    {
        var e = new DeliveryFlaggedAnomalousDomainEvent(
            Guid.NewGuid(), AnomalyType.UnexpectedStop, "Stopped for 40 min", "malki");

        var result = AnomalyIntegrationPublisher.Handle(e);

        await Assert.That(result.Reason).IsEqualTo("Stopped for 40 min");
    }

    [Test]
    public async Task Anomaly_Should_Set_Driver_Location_To_Zero_When_Not_On_Delivery_Aggregate()
    {
        // Driver lat/lng are intentionally zeroed — the aggregate does not carry them
        var e = new DeliveryFlaggedAnomalousDomainEvent(
            Guid.NewGuid(), AnomalyType.EtaExceeded, "ETA breached", "babtouma");

        var result = AnomalyIntegrationPublisher.Handle(e);

        await Assert.That(result.DriverLat).IsEqualTo(0d);
        await Assert.That(result.DriverLng).IsEqualTo(0d);
    }

    // ──────────────────────────────────────────────────────────────
    // PositionIntegrationPublisher
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Position_Should_Map_DriverId_And_DistrictId()
    {
        var driverId = Guid.NewGuid();
        var ts = DateTime.UtcNow;
        var e = new DriverPositionUpdatedDomainEvent(
            driverId,
            Factory.CreatePoint(new Coordinate(36.3, 33.5)),
            ts,
            DistrictId: "mezzeh",
            Name: "Ahmad Hassan",
            ShortName: "Ahmad",
            IsActive: true);

        var result = PositionIntegrationPublisher.Handle(e);

        await Assert.That(result.DriverId).IsEqualTo(driverId);
        await Assert.That(result.DistrictId).IsEqualTo("mezzeh");
    }

    [Test]
    public async Task Position_Should_Extract_Lat_Lng_From_Point()
    {
        // NTS Point: X = lng, Y = lat
        var e = new DriverPositionUpdatedDomainEvent(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(36.27, 33.51)),  // X=lng, Y=lat
            DateTime.UtcNow,
            "kafrsousa", "Omar", "O", true);

        var result = PositionIntegrationPublisher.Handle(e);

        await Assert.That(result.Lng).IsEqualTo(36.27);
        await Assert.That(result.Lat).IsEqualTo(33.51);
    }

    [Test]
    public async Task Position_Should_Hardcode_DeliveryStatus_As_InTransit()
    {
        // Delivery status is not on the Driver aggregate — hardcoded as convention
        var e = new DriverPositionUpdatedDomainEvent(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(36.3, 33.5)),
            DateTime.UtcNow,
            "malki", "Samir", "S", false);

        var result = PositionIntegrationPublisher.Handle(e);

        await Assert.That(result.DeliveryStatus).IsEqualTo("InTransit");
    }

    [Test]
    public async Task Position_Should_Carry_Timestamp()
    {
        var ts = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var e = new DriverPositionUpdatedDomainEvent(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(36.3, 33.5)),
            ts,
            "babtouma", "Youssef", "Y", true);

        var result = PositionIntegrationPublisher.Handle(e);

        await Assert.That(result.Timestamp).IsEqualTo(ts);
    }

    // ──────────────────────────────────────────────────────────────
    // CompletedIntegrationPublisher
    // ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Completed_Should_Return_Integration_Event_When_Driver_And_PickedUpAt_Present()
    {
        var deliveryId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var pickedUpAt = DateTime.UtcNow;
        var deliveredAt = pickedUpAt.AddMinutes(45);
        var e = new DeliveryCompletedDomainEvent(
            deliveryId, deliveredAt, driverId, pickedUpAt, ExpectedDurationSeconds: 3600);

        var result = CompletedIntegrationPublisher.Handle(e);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.DeliveryId).IsEqualTo(deliveryId);
        await Assert.That(result.DriverId).IsEqualTo(driverId);
    }

    [Test]
    public async Task Completed_Should_Return_Null_When_DriverId_Is_Null()
    {
        var e = new DeliveryCompletedDomainEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DriverId: null,
            PickedUpAt: DateTime.UtcNow.AddMinutes(-30),
            ExpectedDurationSeconds: 3600);

        var result = CompletedIntegrationPublisher.Handle(e);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Completed_Should_Return_Null_When_PickedUpAt_Is_Null()
    {
        var e = new DeliveryCompletedDomainEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DriverId: Guid.NewGuid(),
            PickedUpAt: null,
            ExpectedDurationSeconds: 3600);

        var result = CompletedIntegrationPublisher.Handle(e);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Completed_Should_Compute_Actual_Duration_As_DeliveredAt_Minus_PickedUpAt()
    {
        var pickedUpAt = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var deliveredAt = new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc); // 30 min = 1800 s
        var e = new DeliveryCompletedDomainEvent(
            Guid.NewGuid(), deliveredAt, Guid.NewGuid(), pickedUpAt, ExpectedDurationSeconds: 3600);

        var result = CompletedIntegrationPublisher.Handle(e);

        await Assert.That(result!.ActualDurationSeconds).IsEqualTo(1800d);
    }

    [Test]
    public async Task Completed_Should_Carry_Expected_Duration_From_Event()
    {
        var pickedUpAt = DateTime.UtcNow;
        var e = new DeliveryCompletedDomainEvent(
            Guid.NewGuid(), pickedUpAt.AddMinutes(60),
            Guid.NewGuid(), pickedUpAt, ExpectedDurationSeconds: 2700);

        var result = CompletedIntegrationPublisher.Handle(e);

        await Assert.That(result!.ExpectedDurationSeconds).IsEqualTo(2700d);
    }
}
