using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Infrastructure.Data;
using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace GridTrack.IntegrationTests.Abstractions;

public abstract class BaseIntegrationTest
{
    [ClassDataSource<IntegrationTestWebAppFactory>(Shared = SharedType.PerAssembly)]
    public static IntegrationTestWebAppFactory Factory { get; set; } = null!;

    protected static async Task<T> InvokeAsync<T>(object message, CancellationToken ct = default)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<T>(message, ct);
    }

    protected static async Task InvokeAsync(object message, CancellationToken ct = default)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(message, ct);
    }

    protected static async Task<TResult> ResolveAsync<TService, TResult>(
        Func<TService, Task<TResult>> action) where TService : notnull
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    protected static async Task ResetDatabaseAsync()
    {
        var connectionFactory = Factory.Services.GetRequiredService<ISqlConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();

        const string sql = """
                           TRUNCATE TABLE "Deliveries", "Drivers", "H3District", delivery_routes
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

    protected static Task SeedDeliveryRoutesAsync(IEnumerable<DeliveryRoute> routes,
        CancellationToken ct = default)
        => SeedAsync(ctx =>
        {
            ctx.Set<DeliveryRoute>().AddRange(routes);
            return Task.CompletedTask;
        }, ct);
}
