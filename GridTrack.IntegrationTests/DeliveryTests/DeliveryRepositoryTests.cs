using FluentAssertions;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Abstractions;
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

    private static async Task<(IDeliveryRepository Repository, IDeliveryReadService ReadService, IUnitOfWork UnitOfWork)> GetServicesAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        return (
            sp.GetRequiredService<IDeliveryRepository>(),
            sp.GetRequiredService<IDeliveryReadService>(),
            sp.GetRequiredService<IUnitOfWork>()
        );
    }

    [Test]
    [NotInParallel(Order = 20)]
    public async Task AddAsync_Should_Persist_Delivery_Retrievable_Via_ReadService()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery();

        await using var scope = Factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IDeliveryRepository>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
        var readService = sp.GetRequiredService<IDeliveryReadService>();

        await repository.AddAsync(delivery, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();

        var retrieved = await readService.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved!.DeliveryId.Should().Be(delivery.DeliveryId);
        retrieved.DistrictId.Should().Be(delivery.DistrictId);
        retrieved.Status.Should().Be(DeliveryStatus.Created);
    }

    [Test]
    [NotInParallel(Order = 21)]
    public async Task GetByIdAsync_ReadService_Should_Return_Null_When_Not_Found()
    {
        await ResetDatabaseAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var readService = scope.ServiceProvider.GetRequiredService<IDeliveryReadService>();

        var result = await readService.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 22)]
    public async Task UpdateAsync_Should_Persist_Status_Change()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery();
        await SeedAsync(async ctx => await ctx.Set<Delivery>().AddAsync(delivery));

        await using var scope = Factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var readService = sp.GetRequiredService<IDeliveryReadService>();
        var repository = sp.GetRequiredService<IDeliveryRepository>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

        var aggregate = await readService.GetAggregateByIdAsync(delivery.DeliveryId, CancellationToken.None);
        aggregate.Should().NotBeNull();

        var driverId = Guid.NewGuid();
        aggregate!.AssignDriver(driverId);
        aggregate.ClearDomainEvents();

        await repository.UpdateAsync(aggregate, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();

        var retrieved = await readService.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

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

        await using var scope = Factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var readService = sp.GetRequiredService<IDeliveryReadService>();
        var repository = sp.GetRequiredService<IDeliveryRepository>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

        var aggregate = await readService.GetAggregateByIdAsync(delivery.DeliveryId, CancellationToken.None);
        aggregate.Should().NotBeNull();

        aggregate!.MarkDelivered(DateTime.UtcNow);
        aggregate.ClearDomainEvents();

        await repository.UpdateAsync(aggregate, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();

        var retrieved = await readService.GetByIdAsync(delivery.DeliveryId, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(DeliveryStatus.Delivered);
        retrieved.DeliveredAt.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 24)]
    public async Task GetByDistrictAsync_ReadService_Should_Exclude_Terminal_Statuses()
    {
        await ResetDatabaseAsync();

        var active = CreateDelivery(districtId: "h3-active");
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

        await using var scope = Factory.Services.CreateAsyncScope();
        var readService = scope.ServiceProvider.GetRequiredService<IDeliveryReadService>();

        var results = (await readService.GetByDistrictAsync("h3-active", CancellationToken.None)).ToList();

        results.Should().HaveCount(2);
    }

    [Test]
    [NotInParallel(Order = 25)]
    public async Task GetByDistrictAsync_ReadService_Should_Return_Empty_For_Unknown_District()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery(districtId: "h3-known");
        await SeedAsync(async ctx => await ctx.Set<Delivery>().AddAsync(delivery));

        await using var scope = Factory.Services.CreateAsyncScope();
        var readService = scope.ServiceProvider.GetRequiredService<IDeliveryReadService>();

        var results = (await readService.GetByDistrictAsync("h3-unknown", CancellationToken.None)).ToList();

        results.Should().BeEmpty();
    }
}
