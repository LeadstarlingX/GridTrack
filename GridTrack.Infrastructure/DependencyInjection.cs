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
using GridTrack.Infrastructure.H3Service;
using GridTrack.Infrastructure.Hubs;
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
        AddApiVersioning(services);
        
        
        
        return services;
    }
    
    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
     
        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                               throw new ArgumentNullException(nameof(configuration));

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseNetTopologySuite()));
        
        
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        
        // Register Read Services
        services.AddScoped<IAnomalyReadService, AnomalyReadService>();
        services.AddScoped<IDeliveryReadService, DeliveryReadService>();
        services.AddScoped<IDriverReadService, DriverReadService>();
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
        services.AddSignalR();
        services.AddScoped<IDashboardPushService, DashboardPushService>();
        

        return services;
    }

    private static IServiceCollection AddApiVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }
    
}