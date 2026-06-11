using Asp.Versioning;
using Dapper;
using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Infrastructure.Caching;
using GridTrack.Infrastructure.Clock;
using GridTrack.Infrastructure.CQRS.ReadServices;
using GridTrack.Infrastructure.CQRS.Respositories;
using GridTrack.Infrastructure.Data;
using GridTrack.Infrastructure.DbContext;
using GridTrack.Infrastructure.ExternalServices;
using GridTrack.Infrastructure.H3Service;
using GridTrack.Infrastructure.Hubs;
using GridTrack.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;

namespace GridTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IH3GridService, H3GridService>();

        AddPersistence(services, configuration);
        AddCaching(services, configuration);
        AddMySignalR(services);
        AddExternalServices(services, configuration);
        AddSeeding(services);

        return services;
    }
    
    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
     
        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                               throw new ArgumentNullException(nameof(configuration));

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseNetTopologySuite()));
        
        
        // Register Repositories
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        
        // Register Read Services
        services.AddScoped<IAnomalyReadService, AnomalyReadService>();
        services.AddScoped<IAnalyticsReadService, AnalyticsReadService>();
        services.AddScoped<IDeliveryReadService, DeliveryReadService>();
        services.AddScoped<IDistrictReadService, DistrictReadService>();
        services.AddScoped<IDriverReadService, DriverReadService>();
        services.AddScoped<IExportReadService, ExportReadService>();
        services.AddScoped<IForecastReadService, ForecastReadService>();
        services.AddScoped<IHeatmapReadService, HeatmapReadService>();
        

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        
        var dataSource = new NpgsqlDataSourceBuilder(connectionString)
            .UseNetTopologySuite()
            .Build();
        services.AddSingleton<ISqlConnectionFactory>(_ =>
            new SqlConnectionFactory(dataSource));
        

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        return services;
    }
    
    private static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Cache") ??
                                  throw new ArgumentNullException(nameof(configuration));

        services.AddStackExchangeRedisCache(options => options.Configuration = connectionString);
        
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            return ConnectionMultiplexer.Connect(connectionString);
        });

        services.AddSingleton<ICacheService, CacheService>();

        return services;
    }
    
    private static IServiceCollection AddMySignalR(this IServiceCollection services)
    {
        // Serialize enums (AnomalyType, DeliveryStatus) as strings so SignalR payloads
        // match the frontend's string unions instead of emitting integers.
        services.AddSignalR()
            .AddJsonProtocol(o =>
                o.PayloadSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter()));
        services.AddScoped<IDashboardPushService, DashboardPushService>();
        

        return services;
    }

    private static IServiceCollection AddExternalServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IOsrmService, OsrmService>(c =>
        {
            c.BaseAddress = new Uri(configuration["Osrm:BaseUrl"] ?? "http://router.project-osrm.org");
            c.Timeout = TimeSpan.FromSeconds(4);
        });

        services.AddHttpClient<IAnalysisChatService, PythonAnalysisChatService>(c =>
            c.BaseAddress = new Uri(
                configuration["Python:BaseUrl"] ?? "http://localhost:8000"));

        return services;
    }

    private static IServiceCollection AddSeeding(this IServiceCollection services)
    {
        services.AddScoped<DataSeeder>();
        services.AddHostedService<SeedService>();
        return services;
    }


    
}