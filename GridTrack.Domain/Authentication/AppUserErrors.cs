using GridTrack.Domain.Abstractions;

namespace GridTrack.Domain.Authentication;

public static class AppUserErrors
{
    public static readonly Error InvalidId           = new("AppUser.InvalidId",           "User ID cannot be empty.");
    public static readonly Error InvalidUsername     = new("AppUser.InvalidUsername",     "Username cannot be empty.");
    public static readonly Error InvalidPasswordHash = new("AppUser.InvalidPasswordHash", "Password hash cannot be empty.");
    public static readonly Error InvalidRole         = new("AppUser.InvalidRole",         "Role must be 'Observer' or 'GeneralObserver'.");
    public static readonly Error NotFound            = new("AppUser.NotFound",            "User not found.");
    public static readonly Error CannotAssignSectors = new("AppUser.CannotAssignSectors",  "Sectors cannot be assigned to a GeneralObserver.");
}