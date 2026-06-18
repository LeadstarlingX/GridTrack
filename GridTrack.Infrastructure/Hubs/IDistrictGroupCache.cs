namespace GridTrack.Infrastructure.Hubs;

public interface IDistrictGroupCache
{
    Task<IReadOnlyList<Guid>> GetGroupIdsForDistrictAsync(string districtId, CancellationToken ct);
    void Invalidate();
}
