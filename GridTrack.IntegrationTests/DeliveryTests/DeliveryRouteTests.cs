using FluentAssertions;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Domain.Deliveries;
using GridTrack.Infrastructure.Data;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.DeliveryTests;

public class DeliveryRouteTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static IDeliveryReadService GetReadService()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDeliveryReadService>();
    }

    private static Delivery MakeDelivery()
    {
        var d = Delivery.Create(Guid.NewGuid(), Damascus, "mezzeh", DateTime.UtcNow, null).Value;
        d.ClearDomainEvents();
        return d;
    }

    [Test]
    [NotInParallel(Order = 50)]
    public async Task GetRouteAsync_Returns_Empty_When_No_Waypoints()
    {
        await ResetDatabaseAsync();

        var delivery = MakeDelivery();
        await SeedDeliveriesAsync([delivery]);

        var result = await GetReadService().GetRouteAsync(delivery.DeliveryId, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 51)]
    public async Task GetRouteAsync_Returns_Waypoints_In_Sequence_Order()
    {
        await ResetDatabaseAsync();

        var delivery = MakeDelivery();
        await SeedDeliveriesAsync([delivery]);

        var routes = new[]
        {
            new DeliveryRoute { DeliveryId = delivery.DeliveryId, Sequence = 1, Lat = 33.51, Lng = 36.27 },
            new DeliveryRoute { DeliveryId = delivery.DeliveryId, Sequence = 2, Lat = 33.52, Lng = 36.28 },
            new DeliveryRoute { DeliveryId = delivery.DeliveryId, Sequence = 3, Lat = 33.53, Lng = 36.29 },
        };
        await SeedDeliveryRoutesAsync(routes);

        var result = (await GetReadService().GetRouteAsync(delivery.DeliveryId, CancellationToken.None)).ToList();

        result.Should().HaveCount(3);
        result[0].Lat.Should().BeApproximately(33.51, 0.0001);
        result[0].Lng.Should().BeApproximately(36.27, 0.0001);
        result[1].Lat.Should().BeApproximately(33.52, 0.0001);
        result[2].Lat.Should().BeApproximately(33.53, 0.0001);
    }

    [Test]
    [NotInParallel(Order = 52)]
    public async Task GetRouteAsync_Returns_Empty_For_Unknown_DeliveryId()
    {
        await ResetDatabaseAsync();

        var result = await GetReadService().GetRouteAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 53)]
    public async Task GetRouteAsync_Returns_Only_Waypoints_For_Target_Delivery()
    {
        await ResetDatabaseAsync();

        var deliveryA = MakeDelivery();
        var deliveryB = MakeDelivery();
        await SeedDeliveriesAsync([deliveryA, deliveryB]);

        var routes = new[]
        {
            new DeliveryRoute { DeliveryId = deliveryA.DeliveryId, Sequence = 1, Lat = 33.51, Lng = 36.27 },
            new DeliveryRoute { DeliveryId = deliveryB.DeliveryId, Sequence = 1, Lat = 33.99, Lng = 36.99 },
        };
        await SeedDeliveryRoutesAsync(routes);

        var result = (await GetReadService().GetRouteAsync(deliveryA.DeliveryId, CancellationToken.None)).ToList();

        result.Should().HaveCount(1);
        result[0].Lat.Should().BeApproximately(33.51, 0.0001);
    }
}
