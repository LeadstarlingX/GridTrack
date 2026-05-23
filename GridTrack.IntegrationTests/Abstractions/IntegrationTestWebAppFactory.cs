using GridTrack.Api;
using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Infrastructure.Data;
using GridTrack.Infrastructure.DbContext;
using GridTrack.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NSubstitute;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using TUnit.Core.Interfaces;
using Wolverine;


namespace GridTrack.IntegrationTests.Abstractions;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncInitializer
{
    
    public readonly IDateTimeProvider DateTimeProviderMock = Substitute.For<IDateTimeProvider>();

    private readonly PostgreSqlContainer _dbContainer =
        new PostgreSqlBuilder("postgis/postgis:18-3.6")
            .WithPassword("postgres")
            .Build();


    private readonly RedisContainer _redisContainer =
        new RedisBuilder("redis:8.4.0")
            .Build();


    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _redisContainer.StartAsync();

        using var _ = CreateClient();
        
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {   
        
        // Inject test connection strings BEFORE services are configured
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["ConnectionStrings:Cache"]             = _redisContainer.GetConnectionString(),
            });
        });
        
        builder.ConfigureTestServices(services =>
        {
            // ── DbContext ───────────────────────────────────────────────
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(IDbContextFactory<AppDbContext>))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            var connStr = _dbContainer.GetConnectionString();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(connStr, x => x.UseNetTopologySuite()));
            services.AddDbContextFactory<AppDbContext>(opt =>
                opt.UseNpgsql(connStr, x => x.UseNetTopologySuite()));

            // ── IUnitOfWork ─────────────────────────────────────────────
            var uow = services.SingleOrDefault(d => d.ServiceType == typeof(IUnitOfWork));
            if (uow != null) services.Remove(uow);
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

            // ── ISqlConnectionFactory (singleton with prod conn str) ────
            var sqlFactory = services.SingleOrDefault(d => d.ServiceType == typeof(ISqlConnectionFactory));
            if (sqlFactory != null) services.Remove(sqlFactory);
            var dataSource = new NpgsqlDataSourceBuilder(connStr).UseNetTopologySuite().Build();
            services.AddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(dataSource));

            // ── IConnectionMultiplexer (Redis) ──────────────────────────
            var mux = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (mux != null) services.Remove(mux);
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString()));

            // ── Disable SeedService ─────────────────────────────────────
            var seed = services.SingleOrDefault(d => d.ImplementationType == typeof(SeedService));
            if (seed != null) services.Remove(seed);

            // ── Auth ────────────────────────────────────────────────────
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme    = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            // ── Stubs ───────────────────────────────────────────────────
            services.RemoveAll<IForecastingService>();
            services.AddSingleton<IForecastingService>(_ =>
            {
                var stub = Substitute.For<IForecastingService>();
                stub.GetDistrictDemandForecastAsync(Arg.Any<string>(), Arg.Any<DateTime>())
                    .Returns(Task.FromResult<ForecastDto?>(null));
                stub.GetEtaAnomaliesAsync(Arg.Any<IEnumerable<string>>())
                    .Returns(Task.FromResult<IEnumerable<AnomalyAlertDto>>(Array.Empty<AnomalyAlertDto>()));
                return stub;
            });

            services.RemoveAll<IDateTimeProvider>();
            services.AddScoped<IDateTimeProvider>(_ =>
            {
                var mock = Substitute.For<IDateTimeProvider>();
                mock.UtcNow.Returns(_ => DateTime.UtcNow);
                return mock;
            });
        });
        
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _redisContainer.StopAsync();
    }
}