using GridTrack.Domain.Abstractions;

namespace GridTrack.Domain.Authentication;

public sealed class AppUser : BaseEntity
{
    private AppUser() { }

    private AppUser(Guid userId, string username, string passwordHash, string role, Guid[] sectorIds)
    {
        UserId       = userId;
        Username     = username;
        PasswordHash = passwordHash;
        Role         = role;
        SectorIds    = sectorIds;
    }

    public Guid   UserId       { get; private set; }
    public string Username     { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role         { get; private set; } = string.Empty;
    public Guid[] SectorIds    { get; private set; } = [];

    public bool IsGeneralObserver => Role == "GeneralObserver";

    public Result UpdateSectors(Guid[] sectorIds)
    {
        SectorIds = sectorIds ?? [];
        return Result.Success();
    }

    public static Result<AppUser> Create(
        Guid userId, string username, string passwordHash, string role, Guid[] sectorIds)
    {
        if (userId == Guid.Empty)
            return Result.Failure<AppUser>(AppUserErrors.InvalidId);
        if (string.IsNullOrWhiteSpace(username))
            return Result.Failure<AppUser>(AppUserErrors.InvalidUsername);
        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<AppUser>(AppUserErrors.InvalidPasswordHash);
        if (role is not "Observer" and not "GeneralObserver")
            return Result.Failure<AppUser>(AppUserErrors.InvalidRole);

        return Result.Success(new AppUser(userId, username.Trim(), passwordHash, role, sectorIds));
    }
}