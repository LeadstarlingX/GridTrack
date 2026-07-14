namespace GridTrack.Application.Abstractions.Authentication;

public interface ILocalJwtService
{
    /// <param name="districtIds">H3 hex strings for SQL filtering. Null = GeneralObserver.</param>
    /// <param name="sectorIds">DistrictGroup UUIDs for SignalR group join. Null = GeneralObserver.</param>
    string Issue(Guid userId, string role, IReadOnlyList<string>? districtIds, Guid[]? sectorIds);
}