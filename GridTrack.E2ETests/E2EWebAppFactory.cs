using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using GridTrack.Infrastructure.Seeding;
using GridTrack.E2ETests.Abstractions;
using GridTrack.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using TUnit.Core.Interfaces;

namespace GridTrack.E2ETests;


public class E2EWebAppFactory : WebApplicationFactory<Program>, IAsyncInitializer
{
    private const string PythonImageName = "gridtrack-forecasting:e2e";

    private readonly INetwork _network = new NetworkBuilder().Build();

    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder("postgis/postgis:18-3.6")
            .WithPassword("postgres")
            .Build();

    private readonly RedisContainer _redis =
        new RedisBuilder("redis:8.4.0")
            .Build();

    private readonly RabbitMqContainer _rabbit;

    private IContainer? _python;
    private string _rabbitUrl = string.Empty;

    public E2EWebAppFactory()
    {
        _rabbit = new RabbitMqBuilder("rabbitmq:management-alpine")
            .WithUsername("gridtrack")
            .WithPassword("gridtrack")
            .WithNetwork(_network)
            .WithNetworkAliases("rabbitmq")
            .Build();
    }

    // Tries, in order:
    // 1. GRIDTRACK_FORECASTING_PATH env var  (CI + dotnet run via launchSettings.json)
    // 2. Sibling directory convention        (CI checkout layout)
    // 3. Known local path on this machine    (Rider ignores launchSettings.json env vars)
    private static string ResolvePythonRepoPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("GRIDTRACK_FORECASTING_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
            return fromEnv;

        var sibling = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "gridtrack-forecasting"));
        if (Directory.Exists(sibling))
            return sibling;

        const string local = @"D:\CodingProjects\gridtrack-forecasting";
        if (Directory.Exists(local))
            return local;

        throw new InvalidOperationException(
            "Python forecasting repo not found. " +
            $"Set GRIDTRACK_FORECASTING_PATH to its absolute path, or place it at '{sibling}'.");
    }

    // Calls `docker build` directly — no temp tar file, image is cached between runs.
    // Only rebuilds when GRIDTRACK_REBUILD_PYTHON_IMAGE=true or the image doesn't exist yet.
    private static async Task EnsurePythonImageAsync(string repoPath, bool forceRebuild, CancellationToken ct)
    {
        if (!forceRebuild)
        {
            using var check = Process.Start(new ProcessStartInfo("docker", $"image inspect {PythonImageName}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            })!;
            await check.WaitForExitAsync(ct);
            if (check.ExitCode == 0) return;
        }

        using var build = Process.Start(new ProcessStartInfo("docker", $"build -t {PythonImageName} \"{repoPath}\"")
        {
            CreateNoWindow = false,
        })!;
        await build.WaitForExitAsync(ct);

        if (build.ExitCode != 0)
            throw new InvalidOperationException($"'docker build' failed for {PythonImageName}.");
    }

    public async Task InitializeAsync()
    {
        var pythonRepoPath = ResolvePythonRepoPath();
        var rebuildImage   = string.Equals(
            Environment.GetEnvironmentVariable("GRIDTRACK_REBUILD_PYTHON_IMAGE"),
            "true", StringComparison.OrdinalIgnoreCase);

        await Task.WhenAll(
            EnsurePythonImageAsync(pythonRepoPath, rebuildImage, CancellationToken.None),
            _db.StartAsync(),
            _redis.StartAsync(),
            _rabbit.StartAsync());

        // Build the host-accessible URL explicitly — GetConnectionString() on a container
        // with WithNetworkAliases returns the network-internal alias ("rabbitmq"), which
        // the .NET host running on the host machine cannot resolve.
        var rabbitPort = _rabbit.GetMappedPublicPort(5672);
        _rabbitUrl = $"amqp://gridtrack:gridtrack@localhost:{rabbitPort}/";

        _python = new ContainerBuilder(PythonImageName)
            .WithNetwork(_network)
            .WithEnvironment("RABBITMQ_URL",   "amqp://gridtrack:gridtrack@rabbitmq:5672")
            .WithEnvironment("GROQ_API_KEY",   "test-key-triggers-fallback")
            .WithEnvironment("GOOGLE_API_KEY", "test-key-triggers-fallback")
            // WithPortBinding maps the container port to a random host port so the
            // wait strategy (which runs on the host, not inside Docker) can reach it.
            // WithExposedPort alone does not create a host binding on Windows.
            .WithPortBinding(8000, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(8000).ForPath("/health"))
                .UntilHttpRequestIsSucceeded(r => r.ForPort(8000).ForPath("/ready")))
            .Build();

        await _python.StartAsync();

        using var _ = CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _db.GetConnectionString(),
                ["ConnectionStrings:Cache"]             = _redis.GetConnectionString(),
                ["ConnectionStrings:Queue"]             = _rabbitUrl,
                ["Clerk:Authority"]                     = "https://test.clerk.invalid",
                ["Python:BaseUrl"]                      = _python is null ? null
                    : $"http://localhost:{_python.GetMappedPublicPort(8000)}",
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
        });
    }

    public override async ValueTask DisposeAsync()
    {
        if (_python != null) await _python.DisposeAsync();
        await base.DisposeAsync();
        await Task.WhenAll(_db.StopAsync(), _redis.StopAsync(), _rabbit.StopAsync());
        await _network.DeleteAsync();
    }
}
