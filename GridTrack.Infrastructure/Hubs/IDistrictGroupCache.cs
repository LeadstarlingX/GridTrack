namespace GridTrack.Infrastructure.Hubs;

public interface IDistrictGroupCache
{
    Task<IReadOnlyList<Guid>> GetGroupIdsForDistrictAsync(string districtId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetAllGroupIdsAsync(CancellationToken ct);
    void Invalidate();
}
