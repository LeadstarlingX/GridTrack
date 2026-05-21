using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.Errors;

public static class H3ServiceErrors
{
    public static readonly Error LocationNotProvided =  new("LocationNotProvided", "Location was not provided.");
}