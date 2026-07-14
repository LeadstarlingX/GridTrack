using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GridTrack.IntegrationTests.Abstractions;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    // Existing tests use this unchanged — no backward-compat break.
    public const string ValidToken           = "test-valid-user";
    public const string GeneralObserverToken = "test-general-observer";
    public const string ObserverPrefix       = "test-observer|";  // format: test-observer|d1|d2|...

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(header))
        {
            var queryToken = Request.Query["access_token"].ToString();
            if (!string.IsNullOrEmpty(queryToken))
                header = $"Bearer {queryToken}";
        }

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Missing Bearer token"));

        var token  = header["Bearer ".Length..].Trim();
        var claims = BuildClaims(token);

        if (claims is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid or missing token"));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket   = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static Claim[]? BuildClaims(string token) => token switch
    {
        ValidToken => [new Claim(ClaimTypes.NameIdentifier, "test-user-id")],

        GeneralObserverToken =>
        [
            new Claim(ClaimTypes.NameIdentifier, "test-general-observer-id"),
            new Claim("role", "GeneralObserver"),
        ],

        _ when token.StartsWith(ObserverPrefix, StringComparison.Ordinal) =>
            BuildObserverClaims(token[ObserverPrefix.Length..]),

        _ => null
    };

    private static Claim[] BuildObserverClaims(string districtsPart)
    {
        // districtsPart = "d1|d2|d3"
        var districtIds = districtsPart.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var claims      = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-observer-id"),
            new("role", "Observer"),
        };
        claims.AddRange(districtIds.Select(d => new Claim("districtId", d)));
        return [.. claims];
    }
}