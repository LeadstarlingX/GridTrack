using GridTrack.Application.Abstractions.Authentication;

namespace GridTrack.Infrastructure.Authentication;

internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)   => BCrypt.Net.BCrypt.HashPassword(password);
    public bool   Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}