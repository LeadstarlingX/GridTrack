using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.Abstractions.Telemetry;
using GridTrack.Application.Interfaces;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Drivers;

public class UpdateDriverPositionHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Fire_Event_On_Cache_Miss_Then_Db_Load()
    {
        var driver = CreateDriver();
        var readService = new FakeDriverReadService(driver);
        var cache = new FakeCacheService();
        var writeBuffer = new FakeWriteBuffer();
        var publisher = new FakeStreamPublisher();
        var handler = new UpdateDriverPositionHandler();

        var request = new UpdatePositionRequest(driver.DriverId, Factory.CreatePoint(new Coordinate(2, 2)), DateTime.UtcNow);
        var (result, events) = await handler.Handle(
            new UpdateDriverPositionCommand(request), readService, cache, writeBuffer, publisher, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DriverPositionUpdatedDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(writeBuffer.Written.Count).IsEqualTo(1);
        await Assert.That(publisher.PublishCount).IsEqualTo(1);
        await Assert.That(cache.SetCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_Should_Fire_Event_On_Cache_Hit_Without_Db_Load()
    {
        var driver = CreateDriver();
        var readService = new FakeDriverReadService(null);
        var cache = new FakeCacheService(new CachedDriverMetadata(driver.Name, driver.ShortName, driver.DistrictId, driver.IsActive));
        var writeBuffer = new FakeWriteBuffer();
        var publisher = new FakeStreamPublisher();
        var handler = new UpdateDriverPositionHandler();

        var request = new UpdatePositionRequest(driver.DriverId, Factory.CreatePoint(new Coordinate(2, 2)), DateTime.UtcNow);
        var (result, events) = await handler.Handle(
            new UpdateDriverPositionCommand(request), readService, cache, writeBuffer, publisher, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DriverPositionUpdatedDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(writeBuffer.Written.Count).IsEqualTo(1);
        await Assert.That(publisher.PublishCount).IsEqualTo(1);
        await Assert.That(cache.SetCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Driver_Not_Found()
    {
        var readService = new FakeDriverReadService(null);
        var cache = new FakeCacheService();
        var writeBuffer = new FakeWriteBuffer();
        var publisher = new FakeStreamPublisher();
        var handler = new UpdateDriverPositionHandler();

        var request = new UpdatePositionRequest(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(2, 2)), DateTime.UtcNow);
        var (result, events) = await handler.Handle(
            new UpdateDriverPositionCommand(request), readService, cache, writeBuffer, publisher, CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(ApplicationErrors.DriverNotFound);
        await Assert.That(events.Count()).IsEqualTo(0);
        await Assert.That(writeBuffer.Written.Count).IsEqualTo(0);
        await Assert.That(publisher.PublishCount).IsEqualTo(0);
    }

    private static Driver CreateDriver()
        => Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", DateTime.UtcNow, "Ahmad Hassan", "Ahmad").Value;

    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed class FakeDriverReadService(Driver? driver) : IDriverReadService
    {
        public Task<Driver?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(driver);

        public Task<IEnumerable<DriverDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<DriverDto>>([]);

        public Task<IEnumerable<DriverDto>> GetNearestAsync(Point location, int count, CancellationToken ct)
            => Task.FromResult<IEnumerable<DriverDto>>([]);

        public Task<GetDriversResponse> GetAllAsync(string? cursor, string? districtId, string? status, string? search, int pageSize, CancellationToken ct)
            => Task.FromResult(new GetDriversResponse([], null, 0));
    }

    private sealed class FakeCacheService(CachedDriverMetadata? primed = null) : ICacheService
    {
        public int SetCallCount { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (primed is T value) return Task.FromResult<T?>(value);
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            SetCallCount++;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan expiration, CancellationToken cancellationToken = default)
            => factory(cancellationToken);
    }

    private sealed class FakeWriteBuffer : IPositionWriteBuffer
    {
        public List<PositionRecord> Written { get; } = [];

        public void Write(Guid driverId, double lat, double lng, string districtId, DateTime recordedAt)
            => Written.Add(new PositionRecord(driverId, lat, lng, districtId, recordedAt));

        public IReadOnlyList<PositionRecord> Drain() => Written;
    }

    private sealed class FakeStreamPublisher : IPositionStreamPublisher
    {
        public int PublishCount { get; private set; }

        public ValueTask PublishAsync(
            Guid driverId, double lat, double lng,
            string districtId, string name, string shortName, bool isActive,
            DateTime ts, CancellationToken ct)
        {
            PublishCount++;
            return ValueTask.CompletedTask;
        }
    }
}
