namespace GridTrack.Application.Abstractions.Authentication;

public interface ICurrentUser
{
    Guid   UserId            { get; }
    string Role              { get; }
    bool   IsGeneralObserver { get; }

    /// <summary>
    /// Null = no district filter (GeneralObserver or background job).
    /// Empty list = Observer with no sectors assigned (blocks all data).
    /// </summary>
    IReadOnlyList<string>? AllowedDistrictIds { get; }

    /// <summary>DistrictGroup UUIDs for SignalR group auto-join.</summary>
    IReadOnlyList<Guid>? AllowedSectorIds { get; }
}