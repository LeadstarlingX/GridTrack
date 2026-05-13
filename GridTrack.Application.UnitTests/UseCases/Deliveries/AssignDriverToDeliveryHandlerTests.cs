using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
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
        var repository = new FakeDeliveryRepository(delivery);
        var handler = new AssignDriverToDeliveryHandler();

        var (result, events) = await handler.Handle(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(delivery.DeliveryId, Guid.NewGuid())),
            repository,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DeliveryAssignedDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(delivery.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Delivery_Not_Found()
    {
        var repository = new FakeDeliveryRepository(null);
        var handler = new AssignDriverToDeliveryHandler();

        var (result, events) = await handler.Handle(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(Guid.NewGuid(), Guid.NewGuid())),
            repository,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(ApplicationErrors.DeliveryNotFound);
        await Assert.That(events.Count()).IsEqualTo(0);
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

    private sealed class FakeDeliveryRepository : IDeliveryRepository
    {
        private readonly Delivery? _delivery;

        public FakeDeliveryRepository(Delivery? delivery)
        {
            _delivery = delivery;
        }

        public Task<Delivery?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_delivery);

        public Task<IEnumerable<Delivery>> GetActiveByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<Delivery>>(Array.Empty<Delivery>());

        public Task AddAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;

        public Task UpdateAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;
    }
}
