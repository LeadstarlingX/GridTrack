using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace GridTrack.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddMappers()
            .AddMyControllers()
            .AddMyApiVersioning();

        return services;
    }


    private static IServiceCollection AddMyControllers(this IServiceCollection services)
    {
        services.AddControllers(o =>
                o.Filters.Add(new AuthorizeFilter(
                    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())))
            .AddApplicationPart(typeof(DependencyInjection).Assembly);

        return services;
    }


    private static IServiceCollection AddMappers(this IServiceCollection services)
    {
        // services.AddScoped<Mapper>();


        return services;
    }
    
    private static IServiceCollection AddMyApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new QueryStringApiVersionReader("api-version")); // ← fallback
            }).AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            });
        
        return services;
    }
}