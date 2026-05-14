using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace GridTrack.Domain.UnitTests.Deliveries;

public class DeliveryTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Create_Should_Return_Success_And_Raise_DomainEvent()
    {
        var deliveryId = Guid.NewGuid();
        var location = Factory.CreatePoint(new Coordinate(10, 10));

        var result = Delivery.Create(deliveryId, location, "h3-1", DateTime.UtcNow, null);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Status).IsEqualTo(DeliveryStatus.Created);
        await Assert.That(result.Value.DomainEvents.OfType<DeliveryCreatedDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AssignDriver_Should_Set_Status_And_Raise_Event()
    {
        var delivery = CreateDelivery();
        delivery.ClearDomainEvents();

        var result = delivery.AssignDriver(Guid.NewGuid());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.Assigned);
        await Assert.That(delivery.DomainEvents.OfType<DeliveryAssignedDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task MarkPickedUp_Should_Transition_To_PickedUp()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.ClearDomainEvents();

        var result = delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(11, 11)), DateTime.UtcNow);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.PickedUp);
        await Assert.That(delivery.PickedUpAt.HasValue).IsTrue();
        await Assert.That(delivery.DomainEvents.OfType<DeliveryPickedUpDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task UpdateLocation_Should_Transition_To_InTransit_When_PickedUp()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(11, 11)), DateTime.UtcNow);
        delivery.ClearDomainEvents();

        var result = delivery.UpdateLocation(Factory.CreatePoint(new Coordinate(12, 12)), DateTime.UtcNow);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.InTransit);
        await Assert.That(delivery.DomainEvents.OfType<DeliveryLocationUpdatedDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task MarkDelivered_Should_Set_Status_And_Timestamps()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(11, 11)), DateTime.UtcNow);
        delivery.UpdateLocation(Factory.CreatePoint(new Coordinate(12, 12)), DateTime.UtcNow);
        delivery.ClearDomainEvents();

        var timestamp = DateTime.UtcNow;
        var result = delivery.MarkDelivered(timestamp);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.Delivered);
        await Assert.That(delivery.DeliveredAt).IsEqualTo(timestamp);
        await Assert.That(delivery.DomainEvents.OfType<DeliveryCompletedDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task MarkCancelled_Should_Set_Status_And_Reason()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.ClearDomainEvents();

        var timestamp = DateTime.UtcNow;
        var result = delivery.MarkCancelled(timestamp, "client request");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.Cancelled);
        await Assert.That(delivery.CancelledAt).IsEqualTo(timestamp);
        await Assert.That(delivery.AnomalyReason).IsEqualTo("client request");
        await Assert.That(delivery.DomainEvents.OfType<DeliveryCancelledDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task FlagAnomaly_Should_Set_Status_And_Flag()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(11, 11)), DateTime.UtcNow);
        delivery.ClearDomainEvents();

        var result = delivery.FlagAnomaly(AnomalyType.RouteDeviation, "detour");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.Anomalous);
        await Assert.That(delivery.AnomalyFlag).IsTrue();
        await Assert.That(delivery.DomainEvents.OfType<DeliveryFlaggedAnomalousDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task AssignDriver_Should_Fail_When_Cancelled()
    {
        var delivery = CreateDelivery();
        delivery.MarkCancelled(DateTime.UtcNow, "test");

        var result = delivery.AssignDriver(Guid.NewGuid());

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.TerminalStatus);
    }

    [Test]
    public async Task MarkDelivered_Should_Fail_When_Not_In_Transit()
    {
        var delivery = CreateDelivery();

        var result = delivery.MarkDelivered(DateTime.UtcNow);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidStatusForOperation);
    }

    [Test]
    public async Task UpdateLocation_Should_Not_Change_Status_When_InTransit()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(11, 11)), DateTime.UtcNow);
        delivery.UpdateLocation(Factory.CreatePoint(new Coordinate(12, 12)), DateTime.UtcNow);
        delivery.ClearDomainEvents();

        var result = delivery.UpdateLocation(Factory.CreatePoint(new Coordinate(13, 13)), DateTime.UtcNow);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.InTransit);
        await Assert.That(delivery.DomainEvents.OfType<DeliveryLocationUpdatedDomainEvent>().Count()).IsEqualTo(1);
    }
    
    
    [Test]
    public async Task FlagAnomaly_Should_Fail_When_Reason_Is_Empty()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());

        var result = delivery.FlagAnomaly(AnomalyType.EtaExceeded, "");

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidReason);
    }

    [Test]
    public async Task Create_Should_Fail_When_DeliveryId_Is_Empty()
    {
        var location = Factory.CreatePoint(new Coordinate(10, 10));

        var result = Delivery.Create(Guid.Empty, location, "h3-1", DateTime.UtcNow, null);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidDeliveryId);
    }

    [Test]
    public async Task Create_Should_Fail_When_Location_Is_Null()
    {
        var result = Delivery.Create(Guid.NewGuid(), null!, "h3-1", DateTime.UtcNow, null);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidLocation);
    }

    [Test]
    public async Task Create_Should_Fail_When_DistrictId_Is_Empty()
    {
        var location = Factory.CreatePoint(new Coordinate(10, 10));

        var result = Delivery.Create(Guid.NewGuid(), location, "", DateTime.UtcNow, null);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidDistrictId);
    }

    [Test]
    public async Task AssignDriver_Should_Fail_When_DriverId_Is_Empty()
    {
        var delivery = CreateDelivery();

        var result = delivery.AssignDriver(Guid.Empty);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidDriverId);
    }

    [Test]
    public async Task AssignDriver_Should_Fail_When_Status_Is_PickedUp()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(11, 11)), DateTime.UtcNow);

        var result = delivery.AssignDriver(Guid.NewGuid());

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidStatusForOperation);
    }

    [Test]
    public async Task MarkPickedUp_Should_Fail_When_Not_Assigned()
    {
        var delivery = CreateDelivery();

        var result = delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(11, 11)), DateTime.UtcNow);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidStatusForOperation);
    }

    [Test]
    public async Task MarkPickedUp_Should_Fail_When_Location_Is_Null()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());

        var result = delivery.MarkPickedUp(null!, DateTime.UtcNow);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidLocation);
    }

    [Test]
    public async Task UpdateLocation_Should_Fail_When_Not_PickedUp_Or_InTransit()
    {
        var delivery = CreateDelivery();

        var result = delivery.UpdateLocation(Factory.CreatePoint(new Coordinate(12, 12)), DateTime.UtcNow);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidStatusForOperation);
    }

    [Test]
    public async Task UpdateLocation_Should_Fail_When_Location_Is_Null()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(11, 11)), DateTime.UtcNow);

        var result = delivery.UpdateLocation(null!, DateTime.UtcNow);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidLocation);
    }

    [Test]
    public async Task MarkDelivered_Should_Fail_When_Cancelled()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.MarkCancelled(DateTime.UtcNow, "test");

        var result = delivery.MarkDelivered(DateTime.UtcNow);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.TerminalStatus);
    }

    [Test]
    public async Task MarkCancelled_Should_Fail_When_Reason_Is_Empty()
    {
        var delivery = CreateDelivery();

        var result = delivery.MarkCancelled(DateTime.UtcNow, "");

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidReason);
    }

    [Test]
    public async Task TransitionTo_Should_Succeed_When_Same_Status()
    {
        var delivery = CreateDelivery();

        var result = delivery.TransitionTo(DeliveryStatus.Created);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.Created);
    }

    [Test]
    public async Task TransitionTo_Should_Fail_When_Invalid_Transition()
    {
        var delivery = CreateDelivery();

        var result = delivery.TransitionTo(DeliveryStatus.Delivered);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidStatusTransition);
    }

    [Test]
    public async Task Anomalous_Can_Transition_To_InTransit()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.FlagAnomaly(AnomalyType.RouteDeviation, "detour");
        delivery.ClearDomainEvents();

        var result = delivery.TransitionTo(DeliveryStatus.InTransit);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.InTransit);
    }

    [Test]
    public async Task Anomalous_Can_Transition_To_Delivered()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.FlagAnomaly(AnomalyType.RouteDeviation, "detour");
        delivery.ClearDomainEvents();

        var result = delivery.TransitionTo(DeliveryStatus.Delivered);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.Delivered);
    }

    [Test]
    public async Task Full_Lifecycle_Should_Complete_Successfully()
    {
        var delivery = CreateDelivery();
        
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.Created);

        delivery.AssignDriver(Guid.NewGuid());
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.Assigned);

        delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(11, 11)), DateTime.UtcNow);
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.PickedUp);

        delivery.UpdateLocation(Factory.CreatePoint(new Coordinate(12, 12)), DateTime.UtcNow);
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.InTransit);

        delivery.MarkDelivered(DateTime.UtcNow);
        await Assert.That(delivery.Status).IsEqualTo(DeliveryStatus.Delivered);
        await Assert.That(delivery.DeliveredAt.HasValue).IsTrue();
    }
    

    private static Delivery CreateDelivery()
    {
        var result = Delivery.Create(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(10, 10)),
            "h3-1",
            DateTime.UtcNow,
            null);

        return result.Value;
    }
}
