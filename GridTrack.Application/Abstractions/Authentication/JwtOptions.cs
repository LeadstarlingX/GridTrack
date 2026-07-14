namespace GridTrack.Application.Abstractions.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "JwtOptions";

    public string Secret      { get; set; } = string.Empty;
    public int    ExpireHours { get; set; } = 8;
}