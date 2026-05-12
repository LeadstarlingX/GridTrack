using GridTrack.Domain.Abstractions;

namespace GridTrack.Domain.H3Districts;

public static class H3DistrictErrors
{
    public static readonly Error InvalidIndex = new("H3District.InvalidIndex", "H3 index cannot be empty.");
    public static readonly Error InvalidCenterPoint = new("H3District.InvalidCenterPoint", "Center point cannot be null.");
    public static readonly Error InvalidBoundary = new("H3District.InvalidBoundary", "Boundary polygon cannot be null.");
    public static readonly Error InvalidResolution = new("H3District.InvalidResolution", "Resolution must be positive.");
    public static readonly Error InvalidLocation = new("H3District.InvalidLocation", "Location cannot be null.");
    public static readonly Error InvalidRingDistance = new("H3District.InvalidRingDistance", "Ring distance must be positive.");
    public static readonly Error InvalidBoundarySize = new("H3District.InvalidBoundarySize", "Boundary polygon size is invalid.");
    public static readonly Error InvalidExpansion = new("H3District.InvalidExpansion", "Expanded service area did not yield a polygon.");
}
