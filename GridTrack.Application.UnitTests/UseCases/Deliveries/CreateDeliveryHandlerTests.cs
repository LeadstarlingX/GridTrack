using GridTrack.Application.Abstractions.Clock;
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
        var h3GridService = new FakeH3GridService("h3-10");
        var clock = new FakeClock(DateTime.UtcNow);
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
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DeliveryCreatedDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(repository.Added!.DomainEvents.Count).IsEqualTo(0);
    }

    
    [Test]
    public async Task Handle_Should_Return_Failure_When_DriverId_Is_Empty()
    {
        var repository = new CreateDriverHandlerTests.FakeDriverRepository();
        var h3GridService = new FakeH3GridService("h3-10");
        var clock = new FakeClock(DateTime.UtcNow);
        var handler = new CreateDriverHandler();

        var request = new CreateDriverRequest(
            Guid.Empty,
            Factory.CreatePoint(new Coordinate(1, 1)),
            9,
            "h3-1",
            true);

        var (result, events) = await handler.Handle(
            new CreateDriverCommand(request),
            repository,
            h3GridService,
            clock,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DriverErrors.InvalidDriverId);
        await Assert.That(events.Count()).IsEqualTo(0);
    }
    
    
    
    internal sealed class FakeDeliveryRepository : IDeliveryRepository
    {
        public Delivery? Added { get; private set; }

        public Task<Delivery?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Delivery?>(null);

        public Task<IEnumerable<Delivery>> GetActiveByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<Delivery>>(Array.Empty<Delivery>());

        public Task AddAsync(Delivery delivery, CancellationToken ct)
        {
            Added = delivery;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeH3GridService : IH3GridService
    {
        private readonly string _index;

        public FakeH3GridService(string index)
        {
            _index = index;
        }

        public Task<string> GetCellIndexForPointAsync(Point location, int resolution)
            => Task.FromResult(_index);

        public Task<IEnumerable<string>> GetNeighborCellsAsync(string h3Index, int ringDistance)
            => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task<IEnumerable<string>> GenerateGridBoundsAsync(
            double minLat,
            double maxLat,
            double minLng,
            double maxLng,
            int resolution)
            => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
