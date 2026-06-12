using GridTrack.Domain.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.Domain.Drivers;

public sealed class Driver : BaseEntity
{
	private Driver()
	{
	}

	private Driver(Guid driverId, Point location, bool isActive, DateTime lastSeen, string districtId,
	               string name, string shortName, string? carType, string? licensePlate, string? phoneNumber)
	{
		DriverId = driverId;
		Location = location;
		IsActive = isActive;
		LastSeen = lastSeen;
		DistrictId = districtId;
		Name = name;
		ShortName = shortName;
		CarType = carType;
		LicensePlate = licensePlate;
		PhoneNumber = phoneNumber;
	}

	public Guid DriverId { get; private set; }
	public Point Location { get; private set; } = null!;
	public bool IsActive { get; private set; }
	public DateTime LastSeen { get; private set; }
	public string DistrictId { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public string ShortName { get; private set; } = string.Empty;
	public string? CarType { get; private set; }
	public string? LicensePlate { get; private set; }
	public string? PhoneNumber { get; private set; }

	public static Result<Driver> Create(
		Guid driverId,
		Point location,
		string districtId,
		DateTime lastSeen,
		string name,
		string shortName,
		bool isActive = true,
		string? carType = null,
		string? licensePlate = null,
		string? phoneNumber = null)
	{
		if (driverId == Guid.Empty)
		{
			return Result.Failure<Driver>(DriverErrors.InvalidDriverId);
		}

		if (location is null)
		{
			return Result.Failure<Driver>(DriverErrors.InvalidLocation);
		}

		if (string.IsNullOrWhiteSpace(districtId))
		{
			return Result.Failure<Driver>(DriverErrors.InvalidDistrictId);
		}

		if (string.IsNullOrWhiteSpace(name))
		{
			return Result.Failure<Driver>(DriverErrors.InvalidName);
		}

		var driver = new Driver(driverId, location, isActive, lastSeen, districtId, name, shortName, carType, licensePlate, phoneNumber);
		driver.RaiseDomainEvent(new DriverEnteredDistrictDomainEvent(driverId, districtId));
		return Result.Success(driver);
	}

	public Result UpdatePosition(Point newLocation, DateTime timestamp)
	{
		if (newLocation is null)
		{
			return Result.Failure(DriverErrors.InvalidLocation);
		}

		Location = newLocation;
		LastSeen = timestamp;
		RaiseDomainEvent(new DriverPositionUpdatedDomainEvent(DriverId, newLocation, timestamp, DistrictId, Name, ShortName, IsActive));
		return Result.Success();
	}

	public Result SetAvailability(bool active)
	{
		if (IsActive == active)
		{
			return Result.Success();
		}

		IsActive = active;
		RaiseDomainEvent(new DriverAvailabilityChangedDomainEvent(DriverId, active));
		return Result.Success();
	}

	public Result<bool> IsOperationalIn(string h3Index)
	{
		if (string.IsNullOrWhiteSpace(h3Index))
		{
			return Result.Failure<bool>(DriverErrors.InvalidDistrictId);
		}

		var isOperational = IsActive && string.Equals(DistrictId, h3Index, StringComparison.OrdinalIgnoreCase);
		return Result.Success(isOperational);
	}

	public Result DeactivateIfStale(TimeSpan threshold)
	{
		if (!IsActive)
		{
			return Result.Success();
		}

		if (threshold <= TimeSpan.Zero)
		{
			return Result.Failure(DriverErrors.InvalidThreshold);
		}

		if (DateTime.UtcNow - LastSeen <= threshold)
		{
			return Result.Success();
		}

		IsActive = false;
		RaiseDomainEvent(new DriverAvailabilityChangedDomainEvent(DriverId, false));
		return Result.Success();
	}
}