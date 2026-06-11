using System.Diagnostics;
using Asp.Versioning.ApiExplorer;
using GridTrack.Api.Extensions;
using GridTrack.Api.Middlewares;
using GridTrack.Application;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Infrastructure;
using GridTrack.Infrastructure.Hubs;
using GridTrack.Presentation;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace GridTrack.Api;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    private IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        var conn = Configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine($"\n******** Using connection string: {conn} ********\n");

        services.AddApi(Configuration)
            .AddPresentation()
            .AddInfrastructure(Configuration)
            .AddApplication();

        services.AddCors(o => o.AddPolicy("Frontend", b =>
            b.WithOrigins(Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5500")
             .AllowAnyHeader()
             .AllowAnyMethod()
             .AllowCredentials()));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IApiVersionDescriptionProvider provider)
    {
        app.UseSwagger();
        
        
        app.UseSwaggerUI(options =>
        {
            foreach (ApiVersionDescription description in provider.ApiVersionDescriptions)
            {
                string url = $"/swagger/{description.GroupName}/swagger.json";
                string name = description.GroupName.ToUpperInvariant();
                options.SwaggerEndpoint(url, name);
            }
        });
        app.ApplyMigrations();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            
        }

        // app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.UseRouting();

        app.UseCors("Frontend");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/health", () => Results.Ok("ok")).AllowAnonymous();

            endpoints.MapGet("/api/diagnostics/latency", async (
                ISqlConnectionFactory sqlFactory,
                IConnectionMultiplexer redisConn,
                IHttpClientFactory httpFactory,
                IConfiguration config,
                CancellationToken ct) =>
            {
                static async Task<LatencyResult> Measure(Func<Task> fn)
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        await fn();
                        sw.Stop();
                        return new LatencyResult(true, sw.ElapsedMilliseconds, null);
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        return new LatencyResult(false, sw.ElapsedMilliseconds, ex.Message);
                    }
                }

                var postgres = await Measure(async () =>
                {
                    using var conn = sqlFactory.CreateConnection(); // factory already opens
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    cmd.ExecuteScalar();
                });

                var redis = await Measure(async () =>
                {
                    var db = redisConn.GetDatabase();
                    await db.PingAsync();
                });

                var pythonBase = config["Python:BaseUrl"] ?? "http://localhost:8000";
                var python = await Measure(async () =>
                {
                    var client = httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    await client.GetAsync($"{pythonBase}/health", ct);
                });

                var osrmBase = config["Osrm:BaseUrl"] ?? "http://router.project-osrm.org";
                var osrm = await Measure(async () =>
                {
                    var client = httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(8);
                    await client.GetAsync($"{osrmBase}/route/v1/driving/36.2765,33.5138;36.2766,33.5139?overview=false", ct);
                });

                var rabbit = await Measure(async () =>
                {
                    var queueCs = config.GetConnectionString("Queue") ?? "";
                    var uri = new Uri(queueCs);
                    var port = uri.Port > 0 ? uri.Port : (uri.Scheme == "amqps" ? 5671 : 5672);
                    using var tcp = new System.Net.Sockets.TcpClient();
                    await tcp.ConnectAsync(uri.Host, port, ct);
                });

                return Results.Ok(new LatencyResponse(postgres, redis, python, osrm, rabbit));
            }).AllowAnonymous();

            endpoints.MapPost("/api/telemetry/positions", async (
                PositionBatchRequest req,
                IHubContext<DashboardHub> hub,
                IConfiguration cfg,
                HttpContext ctx,
                CancellationToken ct) =>
            {
                var key = cfg["Telemetry:ApiKey"];
                if (!string.IsNullOrEmpty(key) &&
                    (!ctx.Request.Headers.TryGetValue("X-Telemetry-Key", out var provided) || provided != key))
                    return Results.Unauthorized();

                var tasks = req.Events.Select(e =>
                    hub.Clients.All.SendCoreAsync(
                        "DriverPositionUpdated",
                        [new { driverId = e.DriverId, lat = e.Lat, lng = e.Lng, districtId = e.DistrictId }],
                        ct));
                await Task.WhenAll(tasks);
                return Results.Ok(new { accepted = req.Events.Count });
            }).AllowAnonymous();

            endpoints.MapControllers();
            endpoints.MapHub<DashboardHub>("/hubs/dashboard").RequireAuthorization();
        });
    }
}

// lowercase names → minimal-API STJ serializes as-is (no CamelCase policy by default)
file record LatencyResult(bool ok, long ms, string? error);
file record LatencyResponse(LatencyResult postgres, LatencyResult redis, LatencyResult python, LatencyResult osrm, LatencyResult rabbit);
file record PositionEvent(string DriverId, double Lat, double Lng, string DistrictId);
file record PositionBatchRequest(IReadOnlyList<PositionEvent> Events);