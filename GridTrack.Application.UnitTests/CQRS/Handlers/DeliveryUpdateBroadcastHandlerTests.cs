using GridTrack.Application.CQRS.Handlers;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

public class DeliveryUpdateBroadcastHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Should_Broadcast_To_Delivery_District()
    {
        var delivery = CreateDelivery(districtId: "malki");
        var push = new FakeDashboardPushService();
        var readService = new FakeDeliveryReadService(delivery);
        var e = new DeliveryLocationUpdatedDomainEvent(
            delivery.DeliveryId,
            Factory.CreatePoint(new Coordinate(36.28, 33.52)),
            DateTime.UtcNow);

        await DeliveryUpdateBroadcastHandler.Handle(e, readService, push, CancellationToken.None);

        await Assert.That(push.DeliveryCalls).Count().IsEqualTo(1);
        await Assert.That(push.DeliveryCalls[0].DistrictId).IsEqualTo("malki");
    }

    [Test]
    public async Task Handle_Should_Map_Delivery_Aggregate_To_Dto()
    {
        var delivery = CreateDelivery(districtId: "mezzeh");
        var push = new FakeDashboardPushService();
        var readService = new FakeDeliveryReadService(delivery);
        var e = new DeliveryLocationUpdatedDomainEvent(
            delivery.DeliveryId,
            Factory.CreatePoint(new Coordinate(1, 1)),
            DateTime.UtcNow);

        await DeliveryUpdateBroadcastHandler.Handle(e, readService, push, CancellationToken.None);

        var dto = push.DeliveryCalls[0].Dto;
        await Assert.That(dto.DeliveryId).IsEqualTo(delivery.DeliveryId);
        await Assert.That(dto.Status).IsEqualTo(delivery.Status);
        await Assert.That(dto.DistrictId).IsEqualTo("mezzeh");
        await Assert.That(dto.AnomalyFlag).IsEqualTo(delivery.AnomalyFlag);
    }

    [Test]
    public async Task Handle_Should_Not_Broadcast_When_Delivery_Not_Found()
    {
        var push = new FakeDashboardPushService();
        var readService = new FakeDeliveryReadService(null);
        var e = new DeliveryLocationUpdatedDomainEvent(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(1, 1)),
            DateTime.UtcNow);

        await DeliveryUpdateBroadcastHandler.Handle(e, readService, push, CancellationToken.None);

        await Assert.That(push.DeliveryCalls).IsEmpty();
    }

    private static Delivery CreateDelivery(string districtId = "mezzeh")
    {
        var result = Delivery.Create(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(1, 1)),
            districtId,
            DateTime.UtcNow);
        return result.Value;
    }

    private sealed class FakeDeliveryReadService : IDeliveryReadService
    {
        private readonly Delivery? _delivery;
        public FakeDeliveryReadService(Delivery? delivery) => _delivery = delivery;

        public Task<Delivery?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_delivery);

        public Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<DeliveryDto?>(null);

        public Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<DeliveryDto>>(Array.Empty<DeliveryDto>());

        public Task<IEnumerable<RouteWaypointDto>> GetRouteAsync(Guid deliveryId, CancellationToken ct)
            => Task.FromResult<IEnumerable<RouteWaypointDto>>(Array.Empty<RouteWaypointDto>());

        public Task<GetDeliveriesResponse> GetAllPaginatedAsync(
            string? cursor, string? status, string? districtId,
            DateTime? from, DateTime? to, int pageSize, CancellationToken ct)
            => Task.FromResult(new GetDeliveriesResponse([], null, null));
    }
}
