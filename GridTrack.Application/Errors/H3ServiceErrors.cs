using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.Errors;

public static class H3ServiceErrors
{
    public static readonly Error LocationNotProvided = new("LocationNotProvided", "Location was not provided.");
    public static readonly Error InvalidCellIndex = new("H3.InvalidCellIndex", "H3 cell index cannot be empty.");
    public static readonly Error InvalidRingDistance = new("H3.InvalidRingDistance", "Ring distance must be a positive integer.");
}