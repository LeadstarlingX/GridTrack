using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Infrastructure.Data;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GridTrack.Infrastructure.Seeding;

public sealed class SeedService(
    IServiceScopeFactory factory,
    SeedCompletionSignal seedSignal,
    ILogger<SeedService> logger) : BackgroundService
{
    // PositionSimulatorService/AnomalySimulatorService wait on SeedCompletionSignal before
    // touching Drivers/Deliveries — always signal on the way out (success or failure) so they
    // never block forever or start against a mid-delete/mid-insert table.
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            // Brief delay — let EF migrations finish first
            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            await using var scope = factory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Always reseed on startup: DataSeeder anchors its delivery timestamps (including
            // the dense last-12h band that feeds the pickup-density heatmap) to DateTime.UtcNow
            // at the moment it runs. Skipping when data "already exists" left that window frozen
            // at whenever the app was first seeded, going stale on every later restart.
            logger.LogWarning("Reseeding — clearing all seed data and re-seeding relative to now");
            await db.Set<DeliveryRoute>().ExecuteDeleteAsync(ct);
            await db.Set<Delivery>().ExecuteDeleteAsync(ct);
            await db.Set<Driver>().ExecuteDeleteAsync(ct);

            logger.LogInformation("Starting seed…");
            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
            await seeder.SeedAsync(ct);
            logger.LogInformation("Seed complete");
        }
        finally
        {
            seedSignal.MarkComplete();
        }
    }
}
