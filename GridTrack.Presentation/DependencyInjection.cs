using Asp.Versioning;
using FluentValidation;
using GridTrack.Presentation.Validation;
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
        // Only the containerized load-test environment (Docker, no Clerk JWT available) runs
        // anonymous so k6 can hit the endpoints. The integration-test environment KEEPS the
        // global filter on — TestAuthHandler enforces auth there, and the 401 contract tests
        // depend on it. Do NOT add "Testing" back here.
        var aspEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "";
        var requireAuth = !aspEnv.Equals("Docker", StringComparison.OrdinalIgnoreCase);

        // FluentValidation validators for the HTTP request DTOs, run by ValidationActionFilter.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddControllers(o =>
            {
                if (requireAuth)
                    o.Filters.Add(new AuthorizeFilter(
                        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));

                // Validates action arguments → throws ValidationException (→ 400) on failure.
                o.Filters.Add<ValidationActionFilter>();
            })
            .AddApplicationPart(typeof(DependencyInjection).Assembly)
            // Serialize enums as strings to match frontend string unions.
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter()));

        return services;
    }


    private static IServiceCollection AddMappers(this IServiceCollection services) => services;
    
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