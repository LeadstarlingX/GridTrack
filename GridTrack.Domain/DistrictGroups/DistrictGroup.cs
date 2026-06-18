using GridTrack.Domain.Abstractions;

namespace GridTrack.Domain.DistrictGroups;

public sealed class DistrictGroup : BaseEntity
{
    private DistrictGroup() { }

    private DistrictGroup(Guid id, string name, string[] districtIds)
    {
        Id          = id;
        Name        = name;
        DistrictIds = districtIds;
    }

    public Guid     Id          { get; private set; }
    public string   Name        { get; private set; } = string.Empty;
    public string[] DistrictIds { get; private set; } = [];

    public static Result<DistrictGroup> Create(Guid id, string name, string[] districtIds)
    {
        if (id == Guid.Empty)
            return Result.Failure<DistrictGroup>(DistrictGroupErrors.InvalidId);
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<DistrictGroup>(DistrictGroupErrors.EmptyName);
        if (districtIds is null || districtIds.Length == 0)
            return Result.Failure<DistrictGroup>(DistrictGroupErrors.EmptyDistricts);

        return Result.Success(new DistrictGroup(id, name.Trim(), districtIds));
    }

    public Result Update(string name, string[] districtIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(DistrictGroupErrors.EmptyName);
        if (districtIds is null || districtIds.Length == 0)
            return Result.Failure(DistrictGroupErrors.EmptyDistricts);

        Name        = name.Trim();
        DistrictIds = districtIds;
        return Result.Success();
    }
}
