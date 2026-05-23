using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Deliveries;

public class GetDeliveryByIdHandlerTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Handle_Returns_Null_When_Delivery_Not_Found()
    {
        var handler = new GetDeliveryByIdHandler();

        var result = await handler.Handle(
            new GetDeliveryByIdQuery(Guid.NewGuid()),
            new FakeDeliveryReadService(null),
            CancellationToken.None);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Handle_Maps_Delivery_Fields_Correctly()
    {
        var deliveryId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var pickedUpAt = new DateTime(2026, 5, 1, 10, 30, 0, DateTimeKind.Utc);

        var dto = new DeliveryDto
        {
            DeliveryId = deliveryId,
            CurrentLocation = Factory.CreatePoint(new Coordinate(36.2, 33.5)),
            Status = DeliveryStatus.InTransit,
            AssignedDriverId = driverId,
            DistrictId = "h3-district-1",
            AnomalyFlag = false,
            CreatedAt = createdAt,
            PickedUpAt = pickedUpAt,
        };

        var handler = new GetDeliveryByIdHandler();

        var result = await handler.Handle(
            new GetDeliveryByIdQuery(deliveryId),
            new FakeDeliveryReadService(dto),
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(deliveryId);
        await Assert.That(result.Status).IsEqualTo("InTransit");
        await Assert.That(result.DistrictId).IsEqualTo("h3-district-1");
        await Assert.That(result.AssignedDriverId).IsEqualTo(driverId);
        await Assert.That(result.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(result.UpdatedAt).IsEqualTo(pickedUpAt);
        await Assert.That(result.RoutePolyline).IsEmpty();
    }

    [Test]
    public async Task Handle_Uses_DeliveredAt_As_UpdatedAt_When_Delivered()
    {
        var deliveredAt = new DateTime(2026, 5, 1, 11, 30, 0, DateTimeKind.Utc);

        var dto = new DeliveryDto
        {
            DeliveryId = Guid.NewGuid(),
            CurrentLocation = Factory.CreatePoint(new Coordinate(36.2, 33.5)),
            Status = DeliveryStatus.Delivered,
            DistrictId = "h3-district-1",
            AnomalyFlag = false,
            CreatedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            DeliveredAt = deliveredAt,
        };

        var handler = new GetDeliveryByIdHandler();

        var result = await handler.Handle(
            new GetDeliveryByIdQuery(dto.DeliveryId),
            new FakeDeliveryReadService(dto),
            CancellationToken.None);

        await Assert.That(result!.UpdatedAt).IsEqualTo(deliveredAt);
    }

    [Test]
    public async Task Handle_Returns_Null_AssignedDriverId_When_Unassigned()
    {
        var dto = new DeliveryDto
        {
            DeliveryId = Guid.NewGuid(),
            CurrentLocation = Factory.CreatePoint(new Coordinate(36.2, 33.5)),
            Status = DeliveryStatus.Created,
            DistrictId = "h3-district-1",
            CreatedAt = DateTime.UtcNow
        };

        var handler = new GetDeliveryByIdHandler();

        var result = await handler.Handle(
            new GetDeliveryByIdQuery(dto.DeliveryId),
            new FakeDeliveryReadService(dto),
            CancellationToken.None);

        await Assert.That(result!.AssignedDriverId).IsNull();
    }

    // ── Fakes ─────────────────────────────────────────────────────────────

    private sealed class FakeDeliveryReadService(DeliveryDto? returnValue) : IDeliveryReadService
    {
        public Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(returnValue);

        public Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<DeliveryDto>>(Array.Empty<DeliveryDto>());

        public Task<Delivery?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Delivery?>(null);

        public Task<IEnumerable<RouteWaypointDto>> GetRouteAsync(Guid deliveryId, CancellationToken ct)
            => Task.FromResult<IEnumerable<RouteWaypointDto>>(Array.Empty<RouteWaypointDto>());
    }
}
