using FluentAssertions;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Drivers;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DriverTests;

public class DriverRepositoryTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);

    private static Point Damascus  => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));
    private static Point NearPoint => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5230));

    private static Driver CreateDriver(
        Point? location   = null,
        string districtId = "h3-district-01",
        bool isActive     = true)
    {
        var result = Driver.Create(
            Guid.NewGuid(),
            location ?? Damascus,
            districtId,
            DateTime.UtcNow,
            isActive);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    [Test]
    [NotInParallel(Order = 30)]
    public async Task AddAsync_Should_Persist_Driver_Retrievable_Via_ReadService()
    {
        await ResetDatabaseAsync();

        var driver = CreateDriver();

        await using var scope = Factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IDriverRepository>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
        var readService = sp.GetRequiredService<IDriverReadService>();

        await repository.AddAsync(driver, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();

        var aggregate = await readService.GetAggregateByIdAsync(driver.DriverId, CancellationToken.None);

        aggregate.Should().NotBeNull();
        aggregate!.DriverId.Should().Be(driver.DriverId);
        aggregate.DistrictId.Should().Be(driver.DistrictId);
        aggregate.IsActive.Should().BeTrue();
    }

    [Test]
    [NotInParallel(Order = 31)]
    public async Task GetAggregateByIdAsync_Should_Return_Null_When_Not_Found()
    {
        await ResetDatabaseAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var readService = scope.ServiceProvider.GetRequiredService<IDriverReadService>();

        var result = await readService.GetAggregateByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 32)]
    public async Task UpdateAsync_Should_Persist_Availability_Change()
    {
        await ResetDatabaseAsync();

        var driver = CreateDriver(isActive: true);
        await SeedDriversAsync([driver]);

        await using var scope = Factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var readService = sp.GetRequiredService<IDriverReadService>();
        var repository = sp.GetRequiredService<IDriverRepository>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

        var aggregate = await readService.GetAggregateByIdAsync(driver.DriverId, CancellationToken.None);
        aggregate.Should().NotBeNull();

        aggregate!.SetAvailability(false);
        aggregate.ClearDomainEvents();

        await repository.UpdateAsync(aggregate, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();

        var retrieved = await readService.GetAggregateByIdAsync(driver.DriverId, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved!.IsActive.Should().BeFalse();
    }

    [Test]
    [NotInParallel(Order = 33)]
    public async Task UpdateAsync_Should_Persist_Position_Update()
    {
        await ResetDatabaseAsync();

        var driver = CreateDriver(location: Damascus);
        await SeedDriversAsync([driver]);

        await using var scope = Factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var readService = sp.GetRequiredService<IDriverReadService>();
        var repository = sp.GetRequiredService<IDriverRepository>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

        var aggregate = await readService.GetAggregateByIdAsync(driver.DriverId, CancellationToken.None);
        aggregate.Should().NotBeNull();

        var updatedAt = DateTime.UtcNow;
        aggregate!.UpdatePosition(NearPoint, updatedAt);
        aggregate.ClearDomainEvents();

        await repository.UpdateAsync(aggregate, CancellationToken.None);
        await unitOfWork.SaveChangesAsync();

        var retrieved = await readService.GetAggregateByIdAsync(driver.DriverId, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved!.Location.X.Should().BeApproximately(NearPoint.X, precision: 0.0001);
        retrieved.Location.Y.Should().BeApproximately(NearPoint.Y, precision: 0.0001);
        retrieved.LastSeen.Should().BeCloseTo(updatedAt, precision: TimeSpan.FromSeconds(1));
    }
}
