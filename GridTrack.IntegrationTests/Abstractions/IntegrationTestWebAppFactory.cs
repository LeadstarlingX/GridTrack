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
                ["Clerk:Authority"]                     = "https://test.clerk.invalid",
            });
        });
        
        builder.ConfigureTestServices(services =>
        {
            // 2. Disable Clerk JWT backchannel - don't touch scheme registrations
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.Authority = null;
                o.TokenValidationParameters.ValidateIssuerSigningKey = false;
                o.TokenValidationParameters.ValidateIssuer           = false;
                o.TokenValidationParameters.ValidateAudience         = false;
                o.TokenValidationParameters.ValidateLifetime         = false;
                o.BackchannelHttpHandler = new NullBackchannelHandler();
            });

            // 3. Add test scheme on top of existing auth
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme    = "Test";
                o.DefaultScheme             = "Test";
            });

            // 4. Disable SeedService
            var seed = services.SingleOrDefault(d => d.ImplementationType == typeof(SeedService));
            if (seed != null) services.Remove(seed);

            // 5. Stub unimplemented services
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
            
            var mvcBuilder = services.FirstOrDefault(d => 
                d.ServiceType.FullName?.Contains("ApplicationPartManager") == true);
// set a breakpoint here and check if it's null
            
        });
        
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _redisContainer.StopAsync();
    }
}