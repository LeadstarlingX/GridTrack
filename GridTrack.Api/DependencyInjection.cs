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
    
    private static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global limiter: 100 req/min per IP.
            // Loopback (127.0.0.1 / ::1) is exempt — load tests and dev tools run locally.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var remoteIp = context.Connection.RemoteIpAddress;
                if (remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp))
                    return RateLimitPartition.GetNoLimiter("loopback");

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