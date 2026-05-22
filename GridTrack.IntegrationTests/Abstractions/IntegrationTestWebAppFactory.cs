using GridTrack.Api;
using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Infrastructure.DbContext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
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
        
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(IDateTimeProvider));

            services.AddScoped<IDateTimeProvider>(_ =>
            {
                var mock = Substitute.For<IDateTimeProvider>();
                mock.UtcNow.Returns(_ => DateTime.UtcNow);
                return mock;
            });

            // Stub unimplemented services so Wolverine can build all handlers
            services.TryAddSingleton<IForecastingService>(_ =>
            {
                var stub = Substitute.For<IForecastingService>();
                stub.GetDistrictDemandForecastAsync(Arg.Any<string>(), Arg.Any<DateTime>())
                    .Returns(Task.FromResult<ForecastDto?>(null));
                stub.GetEtaAnomaliesAsync(Arg.Any<IEnumerable<string>>())
                    .Returns(Task.FromResult<IEnumerable<AnomalyAlertDto>>(Array.Empty<AnomalyAlertDto>()));
                return stub;
            });
            
            services.Configure<DbContextOptionsBuilder>(options =>
            {
                var descriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
            
                services.AddDbContext<AppDbContext>((sp, opt) =>
                {
                    opt.UseNpgsql(
                        _dbContainer.GetConnectionString(),
                        x => x.UseNetTopologySuite());
                    opt.EnableSensitiveDataLogging();
                });
            });
            
            // services.AddWolverine(options =>
            // {
            //     options.Discovery.IncludeAssembly(typeof(GridTrack.Application.DependencyInjection).Assembly);
            // });
            
        });
        
        Environment.SetEnvironmentVariable("ConnectionStrings:DefaultConnection", _dbContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings:Redis", _redisContainer.GetConnectionString());
        
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _redisContainer.StopAsync();
    }
}