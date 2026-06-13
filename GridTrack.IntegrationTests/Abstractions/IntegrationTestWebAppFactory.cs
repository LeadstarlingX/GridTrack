using GridTrack.Api;
using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Infrastructure.Data;
using GridTrack.Infrastructure.DbContext;
using GridTrack.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

    // Spy on the SignalR push service so tests can assert that domain events raised by
    // command handlers actually dispatch through Wolverine's cascade to the broadcast handlers.
    public readonly IDashboardPushService DashboardPushMock = Substitute.For<IDashboardPushService>();

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
        // Inject test connection strings BEFORE services are configured.
        // ConnectionStrings:Queue must be explicitly nulled so Program.cs sees
        // a blank rabbit string and Wolverine skips RabbitMQ transport.
        // Without this, appsettings.Development.json's "amqp://localhost:5672"
        // leaks through and Wolverine blocks forever trying to connect.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["ConnectionStrings:Cache"]             = _redisContainer.GetConnectionString() + ",allowAdmin=true",
                ["ConnectionStrings:Queue"]             = null,
                ["Clerk:Authority"]                     = "https://test.clerk.invalid",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.Authority = null;
                o.TokenValidationParameters.ValidateIssuerSigningKey = false;
                o.TokenValidationParameters.ValidateIssuer           = false;
                o.TokenValidationParameters.ValidateAudience         = false;
                o.TokenValidationParameters.ValidateLifetime         = false;
                o.BackchannelHttpHandler = new NullBackchannelHandler();
            });

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme    = "Test";
                o.DefaultScheme             = "Test";
            });

            var seed = services.SingleOrDefault(d => d.ImplementationType == typeof(SeedService));
            if (seed != null) services.Remove(seed);

            services.RemoveAll<IDashboardPushService>();
            services.AddSingleton(DashboardPushMock);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _dbContainer.StopAsync();
        await _redisContainer.StopAsync();
    }
}
