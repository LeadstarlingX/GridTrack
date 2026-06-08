using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Deliveries;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Deliveries;

public class CancelDeliveryHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Return_Event_And_Clear_DomainEvents()
    {
        var delivery = CreateDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new CancelDeliveryHandler();

        var (result, events) = await handler.Handle(
            new CancelDeliveryCommand(new CancelDeliveryRequest(
                delivery.DeliveryId, DateTime.UtcNow, "Customer requested cancellation")),
            readService, repository, unitOfWork, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DeliveryCancelledDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(delivery.DomainEvents.Count).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Delivery_Not_Found()
    {
        var readService = new FakeDeliveryReadService(null);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new CancelDeliveryHandler();

        var (result, events) = await handler.Handle(
            new CancelDeliveryCommand(new CancelDeliveryRequest(
                Guid.NewGuid(), DateTime.UtcNow, "reason")),
            readService, repository, unitOfWork, CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(ApplicationErrors.DeliveryNotFound);
        await Assert.That(events.Count()).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Delivery_Is_Already_Delivered()
    {
        // Delivered is a terminal status — MarkCancelled must fail
        var delivery = CreateDeliveredDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new CancelDeliveryHandler();

        var (result, events) = await handler.Handle(
            new CancelDeliveryCommand(new CancelDeliveryRequest(
                delivery.DeliveryId, DateTime.UtcNow, "too late")),
            readService, repository, unitOfWork, CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(events.Count()).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Reason_Is_Empty()
    {
        var delivery = CreateDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var handler = new CancelDeliveryHandler();

        var (result, events) = await handler.Handle(
            new CancelDeliveryCommand(new CancelDeliveryRequest(
                delivery.DeliveryId, DateTime.UtcNow, string.Empty)),
            readService, new FakeDeliveryRepository(),
            new CreateDeliveryHandlerTests.FakeUnitOfWork(), CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(events.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Preserve_Cancellation_Reason_In_Event()
    {
        var delivery = CreateDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var handler = new CancelDeliveryHandler();
        const string reason = "Address not found";

        var (result, events) = await handler.Handle(
            new CancelDeliveryCommand(new CancelDeliveryRequest(
                delivery.DeliveryId, DateTime.UtcNow, reason)),
            readService, new FakeDeliveryRepository(),
            new CreateDeliveryHandlerTests.FakeUnitOfWork(), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        var cancelledEvent = events.OfType<DeliveryCancelledDomainEvent>().Single();
        await Assert.That(cancelledEvent.Reason).IsEqualTo(reason);
    }

    private static Delivery CreateDelivery()
    {
        var result = Delivery.Create(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(36.3, 33.5)),
            "kafrsousa",
            DateTime.UtcNow,
            null);
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    private static Delivery CreateDeliveredDelivery()
    {
        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());
        delivery.MarkPickedUp(Factory.CreatePoint(new Coordinate(36.3, 33.5)), DateTime.UtcNow);
        delivery.MarkDelivered(DateTime.UtcNow.AddMinutes(30));
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

        public Task<GetDeliveriesResponse> GetAllPaginatedAsync(
            string? cursor, string? status, string? districtId,
            DateTime? from, DateTime? to, int pageSize, CancellationToken ct)
            => Task.FromResult(new GetDeliveriesResponse([], null, null));
    }

    private sealed class FakeDeliveryRepository : IDeliveryRepository
    {
        public Task AddAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;
    }
}
