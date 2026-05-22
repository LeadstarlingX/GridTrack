using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Domain.UnitTests.Drivers;

public class DriverTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Create_Should_Return_Success_And_Raise_DomainEvent()
    {
        var result = Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", DateTime.UtcNow, "Ahmad Hassan", "Ahmad");

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
        var result = Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", lastSeen, "Ahmad Hassan", "Ahmad", true);
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


    [Test]
    public async Task Create_Should_Fail_When_DriverId_Is_Empty()
    {
        var result = Driver.Create(Guid.Empty, Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", DateTime.UtcNow, "Ahmad Hassan", "Ahmad");

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DriverErrors.InvalidDriverId);
    }

    [Test]
    public async Task Create_Should_Fail_When_Location_Is_Null()
    {
        var result = Driver.Create(Guid.NewGuid(), null!, "h3-1", DateTime.UtcNow, "Ahmad Hassan", "Ahmad");

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DriverErrors.InvalidLocation);
    }

    [Test]
    public async Task Create_Should_Fail_When_DistrictId_Is_Empty()
    {
        var result = Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "", DateTime.UtcNow, "Ahmad Hassan", "Ahmad");

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DriverErrors.InvalidDistrictId);
    }

    [Test]
    public async Task UpdatePosition_Should_Fail_When_Location_Is_Null()
    {
        var driver = CreateDriver();

        var result = driver.UpdatePosition(null!, DateTime.UtcNow);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(DriverErrors.InvalidLocation);
    }

    [Test]
    public async Task IsOperationalIn_Should_Return_True_When_Active_And_Same_District()
    {
        var driver = CreateDriver();

        var result = driver.IsOperationalIn("h3-1");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();
    }

    [Test]
    public async Task IsOperationalIn_Should_Return_False_When_Inactive()
    {
        var driver = CreateDriver();
        driver.SetAvailability(false);

        var result = driver.IsOperationalIn("h3-1");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();
    }

    [Test]
    public async Task DeactivateIfStale_Should_Not_Raise_Event_When_Already_Inactive()
    {
        var driver = CreateDriver();
        driver.SetAvailability(false);
        driver.ClearDomainEvents();

        var result = driver.DeactivateIfStale(TimeSpan.FromMinutes(15));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(driver.IsActive).IsFalse();
        await Assert.That(driver.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task UpdatePosition_Should_Raise_PositionUpdated_Event()
    {
        var driver = CreateDriver();
        driver.ClearDomainEvents();
        var newLocation = Factory.CreatePoint(new Coordinate(5, 5));
        var timestamp = DateTime.UtcNow;

        var result = driver.UpdatePosition(newLocation, timestamp);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(driver.Location).IsEqualTo(newLocation);
        await Assert.That(driver.LastSeen).IsEqualTo(timestamp);
        await Assert.That(driver.DomainEvents.OfType<DriverPositionUpdatedDomainEvent>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task SetAvailability_Should_Succeed_Without_Error_When_No_Change()
    {
        var driver = CreateDriver();

        var result = driver.SetAvailability(true);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(driver.IsActive).IsTrue();
    }

    [Test]
    public async Task Full_Driver_Lifecycle_Should_Work_Correctly()
    {
        var driver = CreateDriver();
        
        await Assert.That(driver.IsActive).IsTrue();
        await Assert.That(driver.DistrictId).IsEqualTo("h3-1");

        var newLocation = Factory.CreatePoint(new Coordinate(2, 2));
        driver.UpdatePosition(newLocation, DateTime.UtcNow);
        await Assert.That(driver.Location).IsEqualTo(newLocation);

        driver.SetAvailability(false);
        await Assert.That(driver.IsActive).IsFalse();

        driver.SetAvailability(true);
        await Assert.That(driver.IsActive).IsTrue();
    }
    
    
    private static Driver CreateDriver()
    {
        var result = Driver.Create(Guid.NewGuid(), Factory.CreatePoint(new Coordinate(1, 1)), "h3-1", DateTime.UtcNow, "Ahmad Hassan", "Ahmad", true);
        return result.Value;
    }
}
