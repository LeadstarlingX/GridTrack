using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UnitTests.UseCases.Drivers;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Deliveries;

public class CreateDeliveryHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Return_Event_And_Clear_DomainEvents()
    {
        var repository = new FakeDeliveryRepository();
        var readService = new FakeDeliveryReadService();
        var h3GridService = new FakeH3GridService("h3-10");
        var clock = new FakeClock(DateTime.UtcNow);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateDeliveryHandler();

        var request = new CreateDeliveryRequest(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(10, 10)),
            9,
            null,
            null);

        var (result, events) = await handler.Handle(
            new CreateDeliveryCommand(request),
            repository,
            h3GridService,
            clock,
            unitOfWork,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DeliveryCreatedDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(repository.Added!.DomainEvents.Count).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_DriverId_Is_Empty()
    {
        var repository = new CreateDriverHandlerTests.FakeDriverRepository();
        var h3GridService = new FakeH3GridService("h3-10");
        var clock = new FakeClock(DateTime.UtcNow);
        var unitOfWork = new CreateDriverHandlerTests.FakeUnitOfWork();
        var handler = new CreateDriverHandler();

        var request = new CreateDriverRequest(
            Guid.Empty,
            Factory.CreatePoint(new Coordinate(1, 1)),
            9,
            "h3-1",
            "Ahmad Hassan",
            "Ahmad",
            true);

        var (result, events) = await handler.Handle(
            new CreateDriverCommand(request),
            repository,
            h3GridService,
            clock,
            unitOfWork,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DriverErrors.InvalidDriverId);
        await Assert.That(events.Count()).IsEqualTo(0);
    }

    internal sealed class FakeDeliveryRepository : IDeliveryRepository
    {
        public Delivery? Added { get; private set; }

        public Task AddAsync(Delivery delivery, CancellationToken ct)
        {
            Added = delivery;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;
    }

    internal sealed class FakeDeliveryReadService : IDeliveryReadService
    {
        private readonly Dictionary<Guid, Delivery> _store = new();

        public Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<DeliveryDto?>(null);

        public Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<DeliveryDto>>(Array.Empty<DeliveryDto>());

        public Task<Delivery?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_store.GetValueOrDefault(id));

        public Task<IEnumerable<RouteWaypointDto>> GetRouteAsync(Guid deliveryId, CancellationToken ct)
            => Task.FromResult<IEnumerable<RouteWaypointDto>>(Array.Empty<RouteWaypointDto>());

        public void Seed(Delivery delivery) => _store[delivery.DeliveryId] = delivery;
    }

    internal sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SavedCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SavedCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeH3GridService : IH3GridService
    {
        private readonly string _index;

        public FakeH3GridService(string index) => _index = index;

        public Task<string> GetCellAsync(Point location, int resolution)
            => Task.FromResult(_index);

        public Task<IEnumerable<string>> GetGridDiskAsync(string h3Index, int ringDistance)
            => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task<IEnumerable<string>> FillBoundingBoxAsync(
            double minLat, double maxLat, double minLng, double maxLng, int resolution)
            => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public FakeClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }
}
