using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Drivers;

public class ToggleDriverAvailabilityHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Returns_Null_Response_When_Driver_Not_Found()
    {
        var handler = new ToggleDriverAvailabilityHandler();

        var (response, events) = await handler.Handle(
            new ToggleDriverAvailabilityCommand(Guid.NewGuid(), true),
            new FakeDriverRepository(null),
            CancellationToken.None);

        await Assert.That(response).IsNull();
        await Assert.That(events).IsEmpty();
    }

    [Test]
    public async Task Handle_Returns_Available_Status_When_Toggled_Active()
    {
        var driver = CreateDriver(isActive: false);
        var handler = new ToggleDriverAvailabilityHandler();

        var (response, _) = await handler.Handle(
            new ToggleDriverAvailabilityCommand(driver.DriverId, true),
            new FakeDriverRepository(driver),
            CancellationToken.None);

        await Assert.That(response).IsNotNull();
        await Assert.That(response!.Id).IsEqualTo(driver.DriverId.ToString());
        await Assert.That(response.Status).IsEqualTo("available");
    }

    [Test]
    public async Task Handle_Returns_Offline_Status_When_Toggled_Inactive()
    {
        var driver = CreateDriver(isActive: true);
        var handler = new ToggleDriverAvailabilityHandler();

        var (response, _) = await handler.Handle(
            new ToggleDriverAvailabilityCommand(driver.DriverId, false),
            new FakeDriverRepository(driver),
            CancellationToken.None);

        await Assert.That(response).IsNotNull();
        await Assert.That(response!.Status).IsEqualTo("offline");
    }

    [Test]
    public async Task Handle_Cascades_DomainEvent_When_Availability_Changes()
    {
        var driver = CreateDriver(isActive: false);
        var handler = new ToggleDriverAvailabilityHandler();

        var (_, events) = await handler.Handle(
            new ToggleDriverAvailabilityCommand(driver.DriverId, true),
            new FakeDriverRepository(driver),
            CancellationToken.None);

        await Assert.That(events).IsNotEmpty();
    }

    [Test]
    public async Task Handle_Returns_No_Events_When_Availability_Already_Same()
    {
        // SetAvailability is a no-op when status matches — no domain event raised
        var driver = CreateDriver(isActive: true);
        var handler = new ToggleDriverAvailabilityHandler();

        var (response, events) = await handler.Handle(
            new ToggleDriverAvailabilityCommand(driver.DriverId, true),
            new FakeDriverRepository(driver),
            CancellationToken.None);

        await Assert.That(response).IsNotNull();
        await Assert.That(response!.Status).IsEqualTo("available");
        await Assert.That(events).IsEmpty();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static Driver CreateDriver(bool isActive)
    {
        var result = Driver.Create(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(36.2, 33.5)),
            "h3-district-1",
            DateTime.UtcNow,
            isActive);
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    // ── Fakes ─────────────────────────────────────────────────────────────

    private sealed class FakeDriverRepository(Driver? driver) : IDriverRepository
    {
        private Driver? _current = driver;

        public Task<Driver?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_current);

        public Task<IEnumerable<Driver>> GetActiveByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<Driver>>(Array.Empty<Driver>());

        public Task<IEnumerable<Driver>> GetNearestAsync(Point location, int count, CancellationToken ct)
            => Task.FromResult<IEnumerable<Driver>>(Array.Empty<Driver>());

        public Task AddAsync(Driver driver, CancellationToken ct) => Task.CompletedTask;

        public Task UpdateAsync(Driver driver, CancellationToken ct)
        {
            _current = driver;
            return Task.CompletedTask;
        }
    }
}
