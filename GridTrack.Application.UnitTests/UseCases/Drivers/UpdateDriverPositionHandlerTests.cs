using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
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
    public async Task Handle_Should_Return_Event_And_Clear_DomainEvents()
    {
        var driver = CreateDriver();
        var readService = new FakeDriverReadService(driver);
        var repository = new FakeDriverRepository();
        var unitOfWork = new CreateDriverHandlerTests.FakeUnitOfWork();
        var handler = new UpdateDriverPositionHandler();

        var request = new UpdatePositionRequest(driver.DriverId, Factory.CreatePoint(new Coordinate(2, 2)), DateTime.UtcNow);
        var (result, events) = await handler.Handle(
            new UpdateDriverPositionCommand(request),
            readService,
            repository,
            unitOfWork,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(events.OfType<DriverPositionUpdatedDomainEvent>().Count()).IsEqualTo(1);
        await Assert.That(driver.DomainEvents.Count).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_Should_Return_Failure_When_Driver_Not_Found()
    {
        var readService = new FakeDriverReadService(null);
        var repository = new FakeDriverRepository();
        var unitOfWork = new CreateDriverHandlerTests.FakeUnitOfWork();
        var handler = new UpdateDriverPositionHandler();

        var request = new UpdatePositionRequest(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(2, 2)), DateTime.UtcNow);
        var (result, events) = await handler.Handle(
            new UpdateDriverPositionCommand(request),
            readService,
            repository,
            unitOfWork,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(ApplicationErrors.DriverNotFound);
        await Assert.That(events.Count()).IsEqualTo(0);
        await Assert.That(unitOfWork.SavedCount).IsEqualTo(0);
    }

    private static Driver CreateDriver()
    {
        var result = Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", DateTime.UtcNow, "Ahmad Hassan", "Ahmad");
        return result.Value;
    }

    private sealed class FakeDriverReadService : IDriverReadService
    {
        private readonly Driver? _driver;

        public FakeDriverReadService(Driver? driver) => _driver = driver;

        public Task<IEnumerable<DriverDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<DriverDto>>(Array.Empty<DriverDto>());

        public Task<IEnumerable<DriverDto>> GetNearestAsync(Point location, int count, CancellationToken ct)
            => Task.FromResult<IEnumerable<DriverDto>>(Array.Empty<DriverDto>());

        public Task<Driver?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_driver);

        public Task<GetDriversResponse> GetAllAsync(string? cursor, string? districtId, string? status, string? search, int pageSize, CancellationToken ct)
            => Task.FromResult(new GetDriversResponse([], null, 0));
    }

    private sealed class FakeDriverRepository : IDriverRepository
    {
        public Task AddAsync(Driver driver, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Driver driver, CancellationToken ct) => Task.CompletedTask;
    }
}
