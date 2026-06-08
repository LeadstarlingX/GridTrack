using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Deliveries;

public class FlagDeliveryAnomalyHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Return_Event_And_Clear_DomainEvents()
    {
        var delivery = CreateDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new FlagDeliveryAnomalyHandler();

        var (result, domainEvent) = await handler.Handle(
            new FlagDeliveryAnomalyCommand(new FlagAnomalyRequest(
                delivery.DeliveryId, AnomalyType.StalePosition, "No movement for 30 min")),
            readService, repository, unitOfWork, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(domainEvent).IsNotNull();
        await Assert.That(domainEvent!.DeliveryId).IsEqualTo(delivery.DeliveryId);
        await Assert.That(domainEvent.Type).IsEqualTo(AnomalyType.StalePosition);
        await Assert.That(delivery.DomainEvents.Count).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Delivery_Not_Found()
    {
        var readService = new FakeDeliveryReadService(null);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new FlagDeliveryAnomalyHandler();

        var (result, domainEvent) = await handler.Handle(
            new FlagDeliveryAnomalyCommand(new FlagAnomalyRequest(
                Guid.NewGuid(), AnomalyType.RouteDeviation, "reason")),
            readService, repository, unitOfWork, CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(ApplicationErrors.DeliveryNotFound);
        await Assert.That(domainEvent).IsNull();
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Delivery_Is_In_Terminal_Status()
    {
        // Delivered → Anomalous is not a valid transition
        var delivery = CreateDeliveredDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new FlagDeliveryAnomalyHandler();

        var (result, domainEvent) = await handler.Handle(
            new FlagDeliveryAnomalyCommand(new FlagAnomalyRequest(
                delivery.DeliveryId, AnomalyType.EtaExceeded, "already delivered")),
            readService, repository, unitOfWork, CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(domainEvent).IsNull();
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(AnomalyType.StalePosition)]
    [Arguments(AnomalyType.RouteDeviation)]
    [Arguments(AnomalyType.UnexpectedStop)]
    [Arguments(AnomalyType.EtaExceeded)]
    public async Task Handle_Should_Preserve_Anomaly_Type_In_Event(AnomalyType type)
    {
        var delivery = CreateDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var handler = new FlagDeliveryAnomalyHandler();

        var (result, domainEvent) = await handler.Handle(
            new FlagDeliveryAnomalyCommand(new FlagAnomalyRequest(delivery.DeliveryId, type, "reason")),
            readService, new FakeDeliveryRepository(),
            new CreateDeliveryHandlerTests.FakeUnitOfWork(), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(domainEvent!.Type).IsEqualTo(type);
    }

    private static Delivery CreateDelivery()
    {
        var result = Delivery.Create(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(36.3, 33.5)),
            "mezzeh",
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
