using GridTrack.Api;
using GridTrack.Application.Abstractions.Clock;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using TUnit.Core.Interfaces;

namespace GridTrack.IntegrationTests.Abstractions;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncInitializer
{
    // Obsolete constructors will be removed in future versions, keep the parameter constructor
    //  it's enough to pass the image.
    
    public readonly IDateTimeProvider DateTimeProviderMock = Substitute.For<IDateTimeProvider>();

    private readonly MsSqlContainer _dbContainer =
        new MsSqlBuilder("postgis/postgis:18-3.6")
            .WithPassword("postgres")
            .Build();


    private readonly RedisContainer _redisContainer =
        new RedisBuilder("redis:8.4.0")
            .Build();


    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _redisContainer.StartAsync();

        // Force the host to start and apply migrations before any tests run
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