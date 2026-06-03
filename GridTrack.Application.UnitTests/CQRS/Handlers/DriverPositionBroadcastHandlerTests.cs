using GridTrack.Application.CQRS.Handlers;
using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

public class DriverPositionBroadcastHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Broadcast_To_Correct_District()
    {
        var push = new FakeDashboardPushService();
        var e = BuildEvent(districtId: "mezzeh");

        await DriverPositionBroadcastHandler.Handle(e, push, CancellationToken.None);

        await Assert.That(push.DriverCalls).Count().IsEqualTo(1);
        await Assert.That(push.DriverCalls[0].DistrictId).IsEqualTo("mezzeh");
    }

    [Test]
    public async Task Handle_Should_Map_All_Event_Fields_To_Dto()
    {
        var push = new FakeDashboardPushService();
        var driverId = Guid.NewGuid();
        var location = Factory.CreatePoint(new Coordinate(36.24, 33.50));
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var e = new DriverPositionUpdatedDomainEvent(
            driverId, location, timestamp, "malki", "Ahmad Hassan", "Ahmad", true);

        await DriverPositionBroadcastHandler.Handle(e, push, CancellationToken.None);

        var dto = push.DriverCalls[0].Dto;
        await Assert.That(dto.DriverId).IsEqualTo(driverId);
        await Assert.That(dto.Location).IsEqualTo(location);
        await Assert.That(dto.IsActive).IsTrue();
        await Assert.That(dto.LastSeen).IsEqualTo(timestamp);
        await Assert.That(dto.DistrictId).IsEqualTo("malki");
        await Assert.That(dto.Name).IsEqualTo("Ahmad Hassan");
        await Assert.That(dto.ShortName).IsEqualTo("Ahmad");
    }

    [Test]
    public async Task Handle_Should_Propagate_Inactive_Status()
    {
        var push = new FakeDashboardPushService();
        var e = BuildEvent(isActive: false);

        await DriverPositionBroadcastHandler.Handle(e, push, CancellationToken.None);

        await Assert.That(push.DriverCalls[0].Dto.IsActive).IsFalse();
    }

    private static DriverPositionUpdatedDomainEvent BuildEvent(
        string districtId = "mezzeh",
        bool isActive = true)
        => new(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(1, 1)),
            DateTime.UtcNow,
            districtId,
            "Sami Karimi",
            "Sami",
            isActive);
}
