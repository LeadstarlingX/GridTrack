

using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace GridTrack.IntegrationTests.Abstractions;

public abstract class BaseIntegrationTest
{
    
    [ClassDataSource<IntegrationTestWebAppFactory>(Shared = SharedType.PerAssembly)]
    public static IntegrationTestWebAppFactory Factory { get; set; } = null!;
    
    
    
    protected static async Task ResetDatabaseAsync()
    {
        var connectionFactory = Factory.Services.GetRequiredService<ISqlConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();
 
        const string sql = """
                           TRUNCATE TABLE "Deliveries", "Drivers", "H3District"
                           RESTART IDENTITY CASCADE;
                           """;
 
        await connection.ExecuteAsync(sql);
    }
 
    protected static async Task SeedAsync(Func<AppDbContext, Task> seed, CancellationToken ct = default)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
 
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        await seed(context);
        await context.SaveChangesAsync(ct);
    }
 
 
    protected static Task SeedDeliveriesAsync(IEnumerable<Delivery> deliveries,
        CancellationToken ct = default)
        => SeedAsync(ctx =>
        {
            ctx.Set<Delivery>().AddRange(deliveries);
            return Task.CompletedTask;
        }, ct);
 
    protected static Task SeedDriversAsync(IEnumerable<Driver> drivers,
        CancellationToken ct = default)
        => SeedAsync(ctx =>
        {
            ctx.Set<Driver>().AddRange(drivers);
            return Task.CompletedTask;
        }, ct);
    
}