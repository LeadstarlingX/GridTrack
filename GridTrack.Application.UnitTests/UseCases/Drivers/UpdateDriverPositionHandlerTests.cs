using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Drivers;

public class UpdateDriverPositionHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Return_Event_And_Clear_DomainEvents()
    {
        var driver = CreateDriver();
        var repository = new FakeDriverRepository(driver);
        var handler = new UpdateDriverPositionHandler();

        var request = new UpdatePositionRequest(driver.DriverId, Factory.CreatePoint(new Coordinate(2, 2)), DateTime.UtcNow);
        var (result, events) = await handler.Handle(
            new UpdateDriverPositionCommand(request),
            repository,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DriverPositionUpdatedDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(driver.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Driver_Not_Found()
    {
        var repository = new FakeDriverRepository(null);
        var handler = new UpdateDriverPositionHandler();

        var request = new UpdatePositionRequest(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(2, 2)), DateTime.UtcNow);
        var (result, events) = await handler.Handle(
            new UpdateDriverPositionCommand(request),
            repository,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(ApplicationErrors.DriverNotFound);
        await Assert.That(events.Count()).IsEqualTo(0);
    }

    private static Driver CreateDriver()
    {
        var result = Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", DateTime.UtcNow);
        return result.Value;
    }

    private sealed class FakeDriverRepository : IDriverRepository
    {
        private readonly Driver? _driver;

        public FakeDriverRepository(Driver? driver)
        {
            _driver = driver;
        }

        public Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_driver);

        public Task<IEnumerable<Driver>> GetActiveByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<Driver>>(Array.Empty<Driver>());

        public Task<IEnumerable<Driver>> GetNearestAsync(Point location, int count, CancellationToken ct)
            => Task.FromResult<IEnumerable<Driver>>(Array.Empty<Driver>());

        public Task AddAsync(Driver driver, CancellationToken ct) => Task.CompletedTask;

        public Task UpdateAsync(Driver driver, CancellationToken ct) => Task.CompletedTask;
    }
}
