using GridTrack.Domain.Drivers;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GridTrack.Infrastructure.Seeding;

public sealed class SeedService(
    IServiceScopeFactory factory,
    ILogger<SeedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Brief delay — let EF migrations finish first
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        await using var scope = factory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.Set<Driver>().AnyAsync(ct))
        {
            logger.LogInformation("Seed skipped — data already exists");
            return;
        }

        logger.LogInformation("Starting seed…");
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync(ct);
        logger.LogInformation("Seed complete");
    }
}
