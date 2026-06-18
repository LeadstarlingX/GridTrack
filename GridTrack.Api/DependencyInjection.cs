using System.Threading.RateLimiting;
using GridTrack.Api.Extensions;
using GridTrack.Api.OpenApi;
using Wolverine;

namespace GridTrack.Api;

public static class DependencyInjection
{
     public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureOptions<ConfigureSwaggerOptions>();
        services.AddOpenApi();
        services.AddMyMiddlewares();
        services.AddApiSwagger();
        services.AddClerkAuth(configuration);
        services.AddRateLimiting();
        return services;
    }
     

    private static IServiceCollection AddMyMiddlewares(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Enter: Bearer {your Clerk JWT}",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
    
    // Loopback + all RFC1918 ranges (Docker gateway, LAN) bypass the rate limiter.
    // Rate limiting is only meaningful for external internet traffic.
    private static bool IsInternalIp(System.Net.IPAddress ip)
    {
        if (System.Net.IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    private static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global limiter: 100 req/min per IP, internal traffic exempt.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var remoteIp = context.Connection.RemoteIpAddress;
                if (remoteIp is not null && IsInternalIp(remoteIp))
                    return RateLimitPartition.GetNoLimiter("internal");

                var key = remoteIp?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: key,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    });
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
            };
        });

        return services;
    }
}