using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.Errors;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        new("Auth.InvalidCredentials", "Invalid username or password.");

    public static readonly Error LocalAuthNotConfigured =
        new("Auth.LocalAuthNotConfigured", "Local JWT auth is not enabled on this server.");
}