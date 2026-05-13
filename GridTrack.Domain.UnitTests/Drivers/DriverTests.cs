using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Domain.UnitTests.Drivers;

public class DriverTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Create_Should_Return_Success_And_Raise_DomainEvent()
    {
        var result = Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", DateTime.UtcNow);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.DomainEvents.OfType<DriverEnteredDistrictDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task UpdatePosition_Should_Update_Location_And_LastSeen()
    {
        var driver = CreateDriver();
        driver.ClearDomainEvents();
        var location = Factory.CreatePoint(new Coordinate(2, 2));
        var timestamp = DateTime.UtcNow;

        var result = driver.UpdatePosition(location, timestamp);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(driver.Location).IsEqualTo(location);
        await Assert.That(driver.LastSeen).IsEqualTo(timestamp);
        await Assert.That(driver.DomainEvents.OfType<DriverPositionUpdatedDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task SetAvailability_Should_Raise_Event_When_Changed()
    {
        var driver = CreateDriver();
        driver.ClearDomainEvents();

        var result = driver.SetAvailability(false);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(driver.IsActive).IsFalse();
        await Assert.That(driver.DomainEvents.OfType<DriverAvailabilityChangedDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task IsOperationalIn_Should_Return_False_When_District_Differs()
    {
        var driver = CreateDriver();

        var result = driver.IsOperationalIn("h3-2");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();
    }

    [Test]
    public async Task DeactivateIfStale_Should_Deactivate_When_LastSeen_Expired()
    {
        var lastSeen = DateTime.UtcNow.AddMinutes(-20);
        var result = Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", lastSeen, true);
        var driver = result.Value;
        driver.ClearDomainEvents();

        var deactivateResult = driver.DeactivateIfStale(TimeSpan.FromMinutes(15));

        await Assert.That(deactivateResult.IsSuccess).IsTrue();
        await Assert.That(driver.IsActive).IsFalse();
        await Assert.That(driver.DomainEvents.OfType<DriverAvailabilityChangedDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task IsOperationalIn_Should_Fail_When_District_Is_Empty()
    {
        var driver = CreateDriver();

        var result = driver.IsOperationalIn(string.Empty);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DriverErrors.InvalidDistrictId);
    }

    [Test]
    public async Task SetAvailability_Should_Not_Raise_Event_When_Unchanged()
    {
        var driver = CreateDriver();
        driver.ClearDomainEvents();

        var result = driver.SetAvailability(true);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(driver.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DeactivateIfStale_Should_Not_Change_When_Recent()
    {
        var driver = CreateDriver();
        driver.ClearDomainEvents();

        var result = driver.DeactivateIfStale(TimeSpan.FromMinutes(15));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(driver.IsActive).IsTrue();
        await Assert.That(driver.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DeactivateIfStale_Should_Fail_When_Threshold_Is_Invalid()
    {
        var driver = CreateDriver();

        var result = driver.DeactivateIfStale(TimeSpan.Zero);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DriverErrors.InvalidThreshold);
    }

    private static Driver CreateDriver()
    {
        var result = Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", DateTime.UtcNow, true);
        return result.Value;
    }
}
