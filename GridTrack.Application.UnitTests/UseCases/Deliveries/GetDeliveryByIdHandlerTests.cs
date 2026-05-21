using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Deliveries;
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

        var dto = new DeliveryDto(
            deliveryId,
            Factory.CreatePoint(new Coordinate(36.2, 33.5)),
            DeliveryStatus.InTransit,
            driverId,
            null,
            null,
            "h3-district-1",
            false,
            createdAt,
            pickedUpAt,
            null,
            null,
            null);

        var handler = new GetDeliveryByIdHandler();

        var result = await handler.Handle(
            new GetDeliveryByIdQuery(deliveryId),
            new FakeDeliveryReadService(dto),
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(deliveryId.ToString());
        await Assert.That(result.Status).IsEqualTo("InTransit");
        await Assert.That(result.DistrictId).IsEqualTo("h3-district-1");
        await Assert.That(result.AssignedDriverId).IsEqualTo(driverId.ToString());
        await Assert.That(result.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(result.UpdatedAt).IsEqualTo(pickedUpAt);
        await Assert.That(result.RoutePolyline).IsEmpty();
    }

    [Test]
    public async Task Handle_Uses_DeliveredAt_As_UpdatedAt_When_Delivered()
    {
        var deliveredAt = new DateTime(2026, 5, 1, 11, 30, 0, DateTimeKind.Utc);

        var dto = new DeliveryDto(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(36.2, 33.5)),
            DeliveryStatus.Delivered,
            null,
            null,
            null,
            "h3-district-1",
            false,
            new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            null,
            deliveredAt,
            null,
            null);

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
        var dto = new DeliveryDto(
            Guid.NewGuid(),
            Factory.CreatePoint(new Coordinate(36.2, 33.5)),
            DeliveryStatus.Created,
            null,
            null,
            null,
            "h3-district-1",
            false,
            DateTime.UtcNow,
            null,
            null,
            null,
            null);

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
    }
}
