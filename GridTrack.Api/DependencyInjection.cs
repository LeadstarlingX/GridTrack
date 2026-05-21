using System.Threading.RateLimiting;
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
            // // 1. Add this block to define the Bearer token
            // c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            // {
            //     Name = "Authorization",
            //     Description = "Enter your Bearer token in this format: Bearer {token}",
            //     In = ParameterLocation.Header,
            //     Type = SecuritySchemeType.Http,
            //     Scheme = "Bearer",
            //     BearerFormat = "JWT"
            // });
            //
            // // 2. Add this block to apply the security to all requests
            // c.AddSecurityRequirement(new OpenApiSecurityRequirement
            // {
            //     {
            //         new OpenApiSecurityScheme
            //         {
            //             Reference = new OpenApiReference
            //             {
            //                 Type = ReferenceType.SecurityScheme,
            //                 Id = "Bearer"
            //             }
            //         },
            //         new string[] { }
            //     }
            // });
        });

        return services;
    }
    
    private static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // 1. Global Limiter: 100 requests per minute per IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: remoteIp, 
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    });
            });

            // 2. Handle Rejection (429 Too Many Requests)
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