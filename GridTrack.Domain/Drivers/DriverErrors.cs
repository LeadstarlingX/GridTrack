using GridTrack.Domain.Abstractions;

namespace GridTrack.Domain.Drivers;

public static class DriverErrors
{
    public static readonly Error InvalidDriverId = new("Driver.InvalidId", "DriverId cannot be empty.");
    public static readonly Error InvalidLocation = new("Driver.InvalidLocation", "Location cannot be null.");
    public static readonly Error InvalidDistrictId = new("Driver.InvalidDistrictId", "DistrictId cannot be empty.");
    public static readonly Error InvalidThreshold = new("Driver.InvalidThreshold", "Threshold must be positive.");
    public static readonly Error InvalidName = new("Driver.InvalidName", "Driver name cannot be empty.");
}
