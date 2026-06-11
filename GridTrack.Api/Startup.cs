using System.Diagnostics;
using Asp.Versioning.ApiExplorer;
using GridTrack.Api.Extensions;
using GridTrack.Api.Middlewares;
using GridTrack.Application;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Infrastructure;
using GridTrack.Infrastructure.Hubs;
using GridTrack.Presentation;
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
        // app.SeedData();

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
                static async Task<object> Measure(Func<Task> fn)
                {
                    var sw = Stopwatch.StartNew();
                    try { await fn(); sw.Stop(); return new { ok = true, ms = sw.ElapsedMilliseconds }; }
                    catch (Exception ex) { sw.Stop(); return new { ok = false, ms = sw.ElapsedMilliseconds, error = ex.Message }; }
                }

                var postgres = await Measure(async () =>
                {
                    using var conn = (System.Data.Common.DbConnection)sqlFactory.CreateConnection();
                    await conn.OpenAsync(ct);
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    await cmd.ExecuteScalarAsync(ct);
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

                return Results.Ok(new { postgres, redis, python });
            }).AllowAnonymous();

            endpoints.MapControllers();
            endpoints.MapHub<DashboardHub>("/hubs/dashboard").RequireAuthorization();
        });
        
        
    }
}