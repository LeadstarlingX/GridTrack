using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Deliveries;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Deliveries;

public class MarkDeliveryCompletedHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Return_Event_And_Clear_DomainEvents()
    {
        var delivery = CreatePickedUpDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new MarkDeliveryCompletedHandler();

        var (result, events) = await handler.Handle(
            new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(
                delivery.DeliveryId, DateTime.UtcNow.AddMinutes(30))),
            readService, repository, unitOfWork, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DeliveryCompletedDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(delivery.DomainEvents.Count).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Delivery_Not_Found()
    {
        var readService = new FakeDeliveryReadService(null);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new MarkDeliveryCompletedHandler();

        var (result, events) = await handler.Handle(
            new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(
                Guid.NewGuid(), DateTime.UtcNow)),
            readService, repository, unitOfWork, CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(ApplicationErrors.DeliveryNotFound);
        await Assert.That(events.Count()).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Delivery_Not_In_Progress()
    {
        // Created state cannot be completed — must be PickedUp or InTransit
        var delivery = CreateDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new MarkDeliveryCompletedHandler();

        var (result, events) = await handler.Handle(
            new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(
                delivery.DeliveryId, DateTime.UtcNow)),
            readService, repository, unitOfWork, CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(events.Count()).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Set_DeliveredAt_On_Aggregate()
    {
        var delivery = CreatePickedUpDelivery();
        var deliveredAt = DateTime.UtcNow.AddMinutes(45);
        var readService = new FakeDeliveryReadService(delivery);
        var handler = new MarkDeliveryCompletedHandler();

        var (result, events) = await handler.Handle(
            new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(
                delivery.DeliveryId, deliveredAt)),
            readService, new FakeDeliveryRepository(),
            new CreateDeliveryHandlerTests.FakeUnitOfWork(), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        var completedEvent = events.OfType<DeliveryCompletedDomainEvent>().Single();
        await Assert.That(completedEvent.DeliveredAt).IsEqualTo(deliveredAt);
        await Assert.That(completedEvent.DeliveryId).IsEqualTo(delivery.DeliveryId);
    }

    [Test]
    public async Task Handle_Should_Work_When_Delivery_Is_InTransit()
    {
        var delivery = CreateInTransitDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var handler = new MarkDeliveryCompletedHandler();

        var (result, events) = await handler.Handle(
            new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(
                delivery.DeliveryId, DateTime.UtcNow.AddMinutes(60))),
            readService, new FakeDeliveryRepository(),
            new CreateDeliveryHandlerTests.FakeUnitOfWork(), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DeliveryCompletedDomainEvent>().Count()).IsEqualTo(1);
    }

    private static Delivery CreateDelivery()
    {
        var result = Delivery.Create(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(36.3, 33.5)),
            "malki",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1));
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    private static Delivery CreatePickedUpDelivery()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(36.3, 33.5)), DateTime.UtcNow);
        delivery.ClearDomainEvents();
        return delivery;
    }

    private static Delivery CreateInTransitDelivery()
    {
        var delivery = CreatePickedUpDelivery();
        delivery.UpdateLocation(Factory.CreatePoint(new Coordinate(36.31, 33.51)), DateTime.UtcNow);
        delivery.ClearDomainEvents();
        return delivery;
    }

    private sealed class FakeDeliveryReadService : IDeliveryReadService
    {
        private readonly Delivery? _delivery;
        public FakeDeliveryReadService(Delivery? delivery) => _delivery = delivery;

        public Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<DeliveryDto?>(null);

        public Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<DeliveryDto>>(Array.Empty<DeliveryDto>());

        public Task<Delivery?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_delivery);

        public Task<IEnumerable<RouteWaypointDto>> GetRouteAsync(Guid deliveryId, CancellationToken ct)
            => Task.FromResult<IEnumerable<RouteWaypointDto>>(Array.Empty<RouteWaypointDto>());
    }

    private sealed class FakeDeliveryRepository : IDeliveryRepository
    {
        public Task AddAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;
    }
}
