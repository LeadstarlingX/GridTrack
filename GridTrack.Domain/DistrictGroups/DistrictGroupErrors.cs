using GridTrack.Domain.Abstractions;

namespace GridTrack.Domain.DistrictGroups;

public static class DistrictGroupErrors
{
    public static readonly Error InvalidId       = new("DistrictGroup.InvalidId",       "Id cannot be empty.");
    public static readonly Error EmptyName       = new("DistrictGroup.EmptyName",       "Name cannot be empty.");
    public static readonly Error EmptyDistricts  = new("DistrictGroup.EmptyDistricts",  "District group must contain at least one district.");
    public static readonly Error NotFound        = new("DistrictGroup.NotFound",        "District group not found.");
}
