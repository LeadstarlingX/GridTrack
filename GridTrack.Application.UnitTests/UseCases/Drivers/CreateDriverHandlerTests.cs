using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UnitTests.UseCases.Deliveries;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Drivers;

public class CreateDriverHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Return_Event_And_Clear_DomainEvents()
    {
        var repository = new FakeDriverRepository();
        var h3GridService = new FakeH3GridService("h3-10");
        var clock = new FakeClock(DateTime.UtcNow);
        var handler = new CreateDriverHandler();

        var request = new CreateDriverRequest(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(1, 1)),
            9,
            null,
            true);

        var (result, events) = await handler.Handle(
            new CreateDriverCommand(request),
            repository,
            h3GridService,
            clock,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DriverEnteredDistrictDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(repository.Added!.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_DriverId_Is_Empty()
    {
        var repository = new FakeDriverRepository();
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

    [Test]
    public async Task Handle_Should_Return_Failure_When_Location_Is_Null()
    {
        var repository = new CreateDeliveryHandlerTests.FakeDeliveryRepository();
        var h3GridService = new FakeH3GridService("h3-10");
        var clock = new FakeClock(DateTime.UtcNow);
        var handler = new CreateDeliveryHandler();

        var request = new CreateDeliveryRequest(
            Guid.NewGuid(),
            null!,
            9,
            null,
            "h3-1");

        var (result, events) = await handler.Handle(
            new CreateDeliveryCommand(request),
            repository,
            h3GridService,
            clock,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DeliveryErrors.InvalidLocation);
        await Assert.That(events.Count()).IsEqualTo(0);
        await Assert.That(repository.Added).IsNull();
    }

    [Test]
    public async Task Handle_Should_Use_Provided_DistrictId_When_Supplied()
    {
        var repository = new CreateDeliveryHandlerTests.FakeDeliveryRepository();
        var h3GridService = new FakeH3GridService("h3-computed");
        var clock = new FakeClock(DateTime.UtcNow);
        var handler = new CreateDeliveryHandler();

        var request = new CreateDeliveryRequest(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(10, 10)),
            9,
            null,
            "h3-provided");

        var (result, events) = await handler.Handle(
            new CreateDeliveryCommand(request),
            repository,
            h3GridService,
            clock,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.DistrictId).IsEqualTo("h3-provided");
    }

    [Test]
    public async Task Handle_Should_Compute_DistrictId_When_Not_Provided()
    {
        var repository = new CreateDeliveryHandlerTests.FakeDeliveryRepository();
        var h3GridService = new FakeH3GridService("h3-computed");
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
        await Assert.That(result.Value.DistrictId).IsEqualTo("h3-computed");
    }
    
    
    
    internal sealed class FakeDriverRepository : IDriverRepository
    {
        public Driver? Added { get; private set; }

        public Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Driver?>(null);

        public Task<IEnumerable<Driver>> GetActiveByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<Driver>>(Array.Empty<Driver>());

        public Task<IEnumerable<Driver>> GetNearestAsync(Point location, int count, CancellationToken ct)
            => Task.FromResult<IEnumerable<Driver>>(Array.Empty<Driver>());

        public Task AddAsync(Driver driver, CancellationToken ct)
        {
            Added = driver;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Driver driver, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeH3GridService : IH3GridService
    {
        private readonly string _index;

        public FakeH3GridService(string index)
        {
            _index = index;
        }

        public Task<string> GetCellAsync(Point location, int resolution)
            => Task.FromResult(_index);

        public Task<IEnumerable<string>> GetGridDiskAsync(string h3Index, int ringDistance)
            => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task<IEnumerable<string>> FillBoundingBoxAsync(
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
