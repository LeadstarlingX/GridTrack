using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GridTrack.Api.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddClerkAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var aspEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "";

        // Containerized load-test environment: no Clerk JWT is available, but the SignalR
        // hub is [Authorize] + RequireAuthorization() and some endpoints need an
        // authenticated principal. Authenticate every request with a stub identity so k6
        // can exercise the hub and protected routes. NEVER active outside Docker.
        if (aspEnv.Equals("Docker", StringComparison.OrdinalIgnoreCase))
        {
            services
                .AddAuthentication(LoadTestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, LoadTestAuthHandler>(
                    LoadTestAuthHandler.SchemeName, _ => { });
            services.AddAuthorization();
            return services;
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                // ASP.NET fetches and caches the JWKS automatically via Authority
                o.Authority = configuration["Clerk:Authority"];
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = false, // Clerk omits 'aud' by default
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew                = TimeSpan.FromSeconds(30),
                };
                // SignalR sends JWT as query param, not Authorization header
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) &&
                            ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}

// Authenticates every request with a fixed stub principal. Only registered when
// ASPNETCORE_ENVIRONMENT=Docker (load testing) — mirrors the integration tests' TestAuthHandler.
internal sealed class LoadTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "LoadTestBypass";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims   = new[] { new Claim(ClaimTypes.NameIdentifier, "load-test-user") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket   = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
