using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GridTrack.Application.Abstractions.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GridTrack.Infrastructure.Authentication;

internal sealed class LocalJwtService(IOptions<JwtOptions> options) : ILocalJwtService
{
    public string Issue(
        Guid userId,
        string role,
        IReadOnlyList<string>? districtIds,
        Guid[]? sectorIds)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Secret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("role",                    role),
        };

        if (districtIds is not null)
            foreach (var d in districtIds)
                claims.Add(new Claim("districtId", d));

        if (sectorIds is not null)
            foreach (var s in sectorIds)
                claims.Add(new Claim("sectorId", s.ToString()));

        var token = new JwtSecurityToken(
            issuer:             "gridtrack-local",
            audience:           "gridtrack-api",
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(options.Value.ExpireHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}