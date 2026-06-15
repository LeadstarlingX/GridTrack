using Asp.Versioning;
using Dapper;
using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dispatch;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Infrastructure.Caching;
using GridTrack.Infrastructure.Clock;
using GridTrack.Infrastructure.CQRS.ReadServices;
using GridTrack.Infrastructure.CQRS.Respositories;
using GridTrack.Infrastructure.Data;
using GridTrack.Infrastructure.DbContext;
using GridTrack.Infrastructure.Dispatch;
using GridTrack.Infrastructure.ExternalServices;
using GridTrack.Infrastructure.H3Service;
using GridTrack.Infrastructure.Hubs;
using GridTrack.Infrastructure.Seeding;
using GridTrack.Infrastructure.Simulation;
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
        AddSimulation(services, configuration);

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
        // Analytics read service wrapped with Redis cache-aside decorator
        services.AddScoped<AnalyticsReadService>();
        services.AddScoped<IAnalyticsReadService>(sp =>
            new CachedAnalyticsReadService(
                sp.GetRequiredService<AnalyticsReadService>(),
                sp.GetRequiredService<ICacheService>()));
        services.AddScoped<IDeliveryReadService, DeliveryReadService>();
        services.AddScoped<IDistrictReadService, DistrictReadService>();
        services.AddScoped<IDriverReadService, DriverReadService>();
        services.AddScoped<IExportReadService, ExportReadService>();
        services.AddScoped<IForecastReadService, ForecastReadService>();
        services.AddScoped<IHeatmapReadService, HeatmapReadService>();

        services.Configure<DispatchWeightsOptions>(
            configuration.GetSection(DispatchWeightsOptions.SectionName));
        services.AddScoped<IDispatchStrategy, WeightedDispatchStrategy>();

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
        var rawCs = configuration.GetConnectionString("Cache") ??
                    throw new ArgumentNullException(nameof(configuration));

        // Render (and some other providers) supply a redis:// URI.
        // StackExchange.Redis expects plain "host:port[,password=...]" — strip the scheme.
        var connectionString = rawCs.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
            ? rawCs["redis://".Length..]
            : rawCs;

        services.AddStackExchangeRedisCache(options => options.Configuration = connectionString);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var opts = ConfigurationOptions.Parse(connectionString);
            // Don't throw during startup if Redis is momentarily unavailable — retry in background.
            opts.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(opts);
        });

        services.AddSingleton<ICacheService, CacheService>();

        return services;
    }
    
    private static IServiceCollection AddMySignalR(this IServiceCollection services)
    {
        // Serialize enums (AnomalyType, DeliveryStatus) as strings so SignalR payloads
        // match the frontend's string unions instead of emitting integers.
        services.AddSignalR(o =>
            {
                // Default ClientTimeoutInterval is 30s — too short when a browser tab is
                // temporarily frozen by the browser's background-tab throttling. 60s gives
                // the client time to unfreeze and respond to the server keep-alive ping
                // before the server considers the connection dead.
                o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                o.KeepAliveInterval     = TimeSpan.FromSeconds(15);
            })
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
        {
            c.BaseAddress = new Uri(configuration["Python:BaseUrl"] ?? "http://localhost:8000");
            c.Timeout = TimeSpan.FromSeconds(25);
        });

        services.AddHttpClient<IAiRecommendationService, PythonAiRecommendationService>(c =>
        {
            c.BaseAddress = new Uri(configuration["Python:BaseUrl"] ?? "http://localhost:8000");
            c.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHttpClient<IForecastService, PythonForecastService>(c =>
        {
            c.BaseAddress = new Uri(configuration["Python:BaseUrl"] ?? "http://localhost:8000");
            c.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }

    private static IServiceCollection AddSeeding(this IServiceCollection services)
    {
        services.AddScoped<DataSeeder>();
        services.AddHostedService<SeedService>();
        return services;
    }

    private static IServiceCollection AddSimulation(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SimulatorOptions>(configuration.GetSection("Simulation"));
        services.AddHostedService<PositionSimulatorService>();
        return services;
    }


    
}