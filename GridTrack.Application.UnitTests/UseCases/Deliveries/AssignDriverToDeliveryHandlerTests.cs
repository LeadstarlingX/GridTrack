using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Deliveries;

public class AssignDriverToDeliveryHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Return_Event_And_Clear_DomainEvents()
    {
        var delivery = CreateDelivery();
        var readService = new FakeDeliveryReadService(delivery);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new AssignDriverToDeliveryHandler();

        var (result, events) = await handler.Handle(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(delivery.DeliveryId, Guid.NewGuid())),
            readService,
            repository,
            unitOfWork,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DeliveryAssignedDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(delivery.DomainEvents.Count).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Delivery_Not_Found()
    {
        var readService = new FakeDeliveryReadService(null);
        var repository = new FakeDeliveryRepository();
        var unitOfWork = new CreateDeliveryHandlerTests.FakeUnitOfWork();
        var handler = new AssignDriverToDeliveryHandler();

        var (result, events) = await handler.Handle(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(Guid.NewGuid(), Guid.NewGuid())),
            readService,
            repository,
            unitOfWork,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(ApplicationErrors.DeliveryNotFound);
        await Assert.That(events.Count()).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(0);
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
