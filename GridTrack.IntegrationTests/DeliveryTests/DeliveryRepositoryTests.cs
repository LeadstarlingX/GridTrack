using FluentAssertions;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DeliveryTests;

public class DeliveryRepositoryTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Delivery CreateDelivery(string districtId = "h3-district-01")
    {
        var result = Delivery.Create(
            Guid.NewGuid(),
            Damascus,
            districtId,
            DateTime.UtcNow,
            expectedEta: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    private static async Task<IDeliveryRepository> GetRepositoryAsync()
    {
        var scope = Factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<IDeliveryRepository>();
    }

    // ── AddAsync + GetByIdAsync ───────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 20)]
    public async Task AddAsync_Then_GetByIdAsync_Should_Persist_And_Retrieve_Delivery()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery();

        await SeedAsync(async ctx =>
        {
            await ctx.Set<Delivery>().AddAsync(delivery);
        });

        var repository = await GetRepositoryAsync();
        var retrieved = await repository.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved!.DeliveryId.Should().Be(delivery.DeliveryId);
        retrieved.DistrictId.Should().Be(delivery.DistrictId);
        retrieved.Status.Should().Be(DeliveryStatus.Created);
    }

    [Test]
    [NotInParallel(Order = 21)]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        await ResetDatabaseAsync();

        var repository = await GetRepositoryAsync();
        var result = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 22)]
    public async Task UpdateAsync_Should_Persist_Status_Change()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery();
        await SeedAsync(async ctx => await ctx.Set<Delivery>().AddAsync(delivery));

        // Mutate: assign a driver (Created → Assigned)
        var driverId = Guid.NewGuid();
        delivery.AssignDriver(driverId);
        delivery.ClearDomainEvents();

        await SeedAsync(ctx =>
        {
            ctx.Set<Delivery>().Update(delivery);
            return Task.CompletedTask;
        });

        var repository = await GetRepositoryAsync();
        var retrieved = await repository.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(DeliveryStatus.Assigned);
        retrieved.AssignedDriverId.Should().Be(driverId);
    }

    [Test]
    [NotInParallel(Order = 23)]
    public async Task UpdateAsync_Should_Persist_Delivery_Completion()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery();
        delivery.AssignDriver(Guid.NewGuid());

        var location = GeoFactory.CreatePoint(new Coordinate(36.28, 33.52));
        delivery.MarkPickedUp(location, DateTime.UtcNow);
        delivery.UpdateLocation(location, DateTime.UtcNow);
        delivery.ClearDomainEvents();

        await SeedAsync(async ctx => await ctx.Set<Delivery>().AddAsync(delivery));

        var completedAt = DateTime.UtcNow;
        delivery.MarkDelivered(completedAt);
        delivery.ClearDomainEvents();

        await SeedAsync(ctx =>
        {
            ctx.Set<Delivery>().Update(delivery);
            return Task.CompletedTask;
        });

        var repository = await GetRepositoryAsync();
        var retrieved = await repository.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(DeliveryStatus.Delivered);
        retrieved.DeliveredAt.Should().NotBeNull();
    }

    // ── GetActiveByDistrictAsync ──────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 24)]
    public async Task GetActiveByDistrictAsync_Should_Exclude_Terminal_Statuses()
    {
        await ResetDatabaseAsync();

        var active    = CreateDelivery(districtId: "h3-active");
        var cancelled = CreateDelivery(districtId: "h3-active");
        cancelled.AssignDriver(Guid.NewGuid());

        var loc = GeoFactory.CreatePoint(new Coordinate(36.28, 33.52));
        cancelled.MarkPickedUp(loc, DateTime.UtcNow);
        cancelled.UpdateLocation(loc, DateTime.UtcNow);
        cancelled.MarkCancelled(DateTime.UtcNow, "Customer cancelled");
        cancelled.ClearDomainEvents();
        active.ClearDomainEvents();

        await SeedAsync(async ctx =>
        {
            await ctx.Set<Delivery>().AddRangeAsync(active, cancelled);
        });

        var repository = await GetRepositoryAsync();
        var results = (await repository.GetActiveByDistrictAsync("h3-active", CancellationToken.None))
            .ToList();

        results.Should().HaveCount(1);
        results[0].DeliveryId.Should().Be(active.DeliveryId);
    }

    [Test]
    [NotInParallel(Order = 25)]
    public async Task GetActiveByDistrictAsync_Should_Exclude_Delivered_Deliveries()
    {
        await ResetDatabaseAsync();

        var active    = CreateDelivery(districtId: "h3-delivered");
        var delivered = CreateDelivery(districtId: "h3-delivered");

        delivered.AssignDriver(Guid.NewGuid());
        var loc = GeoFactory.CreatePoint(new Coordinate(36.28, 33.52));
        delivered.MarkPickedUp(loc, DateTime.UtcNow);
        delivered.UpdateLocation(loc, DateTime.UtcNow);
        delivered.MarkDelivered(DateTime.UtcNow);
        delivered.ClearDomainEvents();
        active.ClearDomainEvents();

        await SeedAsync(async ctx =>
        {
            await ctx.Set<Delivery>().AddRangeAsync(active, delivered);
        });

        var repository = await GetRepositoryAsync();
        var results = (await repository.GetActiveByDistrictAsync("h3-delivered", CancellationToken.None))
            .ToList();

        results.Should().HaveCount(1);
        results[0].DeliveryId.Should().Be(active.DeliveryId);
    }

    [Test]
    [NotInParallel(Order = 26)]
    public async Task GetActiveByDistrictAsync_Should_Return_Empty_For_Unknown_District()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery(districtId: "h3-known");
        await SeedAsync(async ctx => await ctx.Set<Delivery>().AddAsync(delivery));

        var repository = await GetRepositoryAsync();
        var results = (await repository.GetActiveByDistrictAsync("h3-unknown", CancellationToken.None))
            .ToList();

        results.Should().BeEmpty();
    }
}