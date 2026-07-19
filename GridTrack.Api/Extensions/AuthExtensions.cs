using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using GridTrack.Application.Abstractions.Authentication;
using GridTrack.Infrastructure.Authentication;
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
        // Local HS256 JWT path — takes priority when Secret is configured (including Docker).
        // Allows the RBAC demo to work inside Docker with real sector-scoped tokens.
        var jwtSecret = configuration["JwtOptions:Secret"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(jwtSecret))
        {
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    // Keep claim names as written in the token ("role", "districtId", "sectorId").
                    // Without this the middleware remaps "role" → ClaimTypes.Role, breaking CurrentUser.
                    o.MapInboundClaims = false;
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer           = true,
                        ValidIssuer              = "gridtrack-local",
                        ValidateAudience         = true,
                        ValidAudience            = "gridtrack-api",
                        ValidateLifetime         = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey         = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtSecret)),
                        ClockSkew                = TimeSpan.FromSeconds(30),
                    };
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

        // Docker load-test fallback: no JWT secret configured, bypass auth so k6 can
        // exercise protected routes without real tokens. NEVER active outside Docker.
        var aspEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "";
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
