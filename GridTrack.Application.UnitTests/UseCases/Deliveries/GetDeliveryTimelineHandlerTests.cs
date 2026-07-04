using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;

namespace GridTrack.Application.UnitTests.UseCases.Deliveries;

public class GetDeliveryTimelineHandlerTests
{
    private readonly GetDeliveryTimelineHandler _handler = new();

    [Test]
    public async Task Handle_Returns_Null_When_Delivery_Not_Found()
    {
        var result = await _handler.Handle(
            new GetDeliveryTimelineQuery(Guid.NewGuid()),
            new FakeReadService(null),
            CancellationToken.None);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Handle_Always_Includes_Created_Event()
    {
        var dto = MakeDto();
        var result = await _handler.Handle(
            new GetDeliveryTimelineQuery(dto.DeliveryId),
            new FakeReadService(dto),
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Events.Any(e => e.Type == "Created")).IsTrue();
    }

    [Test]
    public async Task Handle_Adds_Assigned_Event_When_Driver_Assigned()
    {
        var dto = MakeDto(assignedDriverId: Guid.NewGuid());
        var result = await CallHandler(dto);

        await Assert.That(result!.Events.Any(e => e.Type == "Assigned")).IsTrue();
    }

    [Test]
    public async Task Handle_Does_Not_Add_Assigned_Event_When_No_Driver()
    {
        var dto = MakeDto();
        var result = await CallHandler(dto);

        await Assert.That(result!.Events.Any(e => e.Type == "Assigned")).IsFalse();
    }

    [Test]
    public async Task Handle_Adds_PickedUp_Event_When_PickedUpAt_Set()
    {
        var dto = MakeDto(pickedUpAt: DateTime.UtcNow.AddMinutes(-20));
        var result = await CallHandler(dto);

        await Assert.That(result!.Events.Any(e => e.Type == "PickedUp")).IsTrue();
    }

    [Test]
    public async Task Handle_Adds_InTransit_Event_For_Active_Transit()
    {
        var dto = MakeDto(
            status: DeliveryStatus.InTransit,
            deliveredAt: null,
            cancelledAt: null);
        var result = await CallHandler(dto);

        await Assert.That(result!.Events.Any(e => e.Type == "InTransit")).IsTrue();
    }

    [Test]
    public async Task Handle_Does_Not_Add_InTransit_When_Already_Delivered()
    {
        var dto = MakeDto(status: DeliveryStatus.Delivered, deliveredAt: DateTime.UtcNow);
        var result = await CallHandler(dto);

        await Assert.That(result!.Events.Any(e => e.Type == "InTransit")).IsFalse();
    }

    [Test]
    public async Task Handle_Adds_Delivered_Event_With_On_Time_Note()
    {
        var eta = DateTime.UtcNow;
        // Delivered 10 min before ETA → "10 min ahead of ETA"
        var dto = MakeDto(
            status: DeliveryStatus.Delivered,
            deliveredAt: eta.AddMinutes(-10),
            expectedEta: eta);
        var result = await CallHandler(dto);

        var deliveredEvent = result!.Events.Single(e => e.Type == "Delivered");
        await Assert.That(deliveredEvent.Note).Contains("ahead of ETA");
    }

    [Test]
    public async Task Handle_Adds_Delivered_Event_With_Late_Note()
    {
        var eta = DateTime.UtcNow;
        // Delivered 15 min after ETA → "15 min late"
        var dto = MakeDto(
            status: DeliveryStatus.Delivered,
            deliveredAt: eta.AddMinutes(15),
            expectedEta: eta);
        var result = await CallHandler(dto);

        var deliveredEvent = result!.Events.Single(e => e.Type == "Delivered");
        await Assert.That(deliveredEvent.Note).Contains("late");
    }

    [Test]
    public async Task Handle_Adds_Delivered_Event_With_No_Note_When_No_Eta()
    {
        var dto = MakeDto(
            status: DeliveryStatus.Delivered,
            deliveredAt: DateTime.UtcNow,
            expectedEta: null);
        var result = await CallHandler(dto);

        var deliveredEvent = result!.Events.Single(e => e.Type == "Delivered");
        await Assert.That(deliveredEvent.Note).IsNull();
    }

    [Test]
    public async Task Handle_Adds_Cancelled_Event_When_CancelledAt_Set()
    {
        var dto = MakeDto(
            status: DeliveryStatus.Cancelled,
            cancelledAt: DateTime.UtcNow,
            anomalyReason: "Customer request");
        var result = await CallHandler(dto);

        var cancelledEvent = result!.Events.Single(e => e.Type == "Cancelled");
        await Assert.That(cancelledEvent.Note).IsEqualTo("Customer request");
    }

    [Test]
    public async Task Handle_Adds_AnomalyFlagged_Event_With_EtaExceeded_Label()
    {
        var dto = MakeDto(anomalyFlag: true, anomalyType: AnomalyType.EtaExceeded);
        var result = await CallHandler(dto);

        var anomalyEvent = result!.Events.Single(e => e.Type == "AnomalyFlagged");
        await Assert.That(anomalyEvent.Label).IsEqualTo("ETA exceeded");
    }

    [Test]
    public async Task Handle_Adds_AnomalyFlagged_Event_With_RouteDeviation_Label()
    {
        var dto = MakeDto(anomalyFlag: true, anomalyType: AnomalyType.RouteDeviation);
        var result = await CallHandler(dto);

        var anomalyEvent = result!.Events.Single(e => e.Type == "AnomalyFlagged");
        await Assert.That(anomalyEvent.Label).IsEqualTo("Route deviation");
    }

    [Test]
    public async Task Handle_Adds_AnomalyFlagged_Event_With_UnexpectedStop_Label()
    {
        var dto = MakeDto(anomalyFlag: true, anomalyType: AnomalyType.UnexpectedStop);
        var result = await CallHandler(dto);

        var anomalyEvent = result!.Events.Single(e => e.Type == "AnomalyFlagged");
        await Assert.That(anomalyEvent.Label).IsEqualTo("Unexpected stop");
    }

    [Test]
    public async Task Handle_Adds_AnomalyFlagged_Event_With_Default_Label_For_Null_Type()
    {
        var dto = MakeDto(anomalyFlag: true, anomalyType: null);
        var result = await CallHandler(dto);

        var anomalyEvent = result!.Events.Single(e => e.Type == "AnomalyFlagged");
        await Assert.That(anomalyEvent.Label).IsEqualTo("Anomaly detected");
    }

    [Test]
    public async Task Handle_Sets_AnomalyAt_To_CancelledAt_For_Cancelled_Deliveries()
    {
        var cancelledAt = DateTime.UtcNow;
        var dto = MakeDto(
            status: DeliveryStatus.Cancelled,
            cancelledAt: cancelledAt,
            anomalyFlag: true,
            anomalyType: AnomalyType.EtaExceeded);
        var result = await CallHandler(dto);

        var anomalyEvent = result!.Events.Single(e => e.Type == "AnomalyFlagged");
        await Assert.That(anomalyEvent.At).IsEqualTo(cancelledAt);
    }

    [Test]
    public async Task Handle_Returns_DeliveryId_In_Response()
    {
        var id = Guid.NewGuid();
        var dto = MakeDto(id: id);
        var result = await CallHandler(dto);

        await Assert.That(result!.DeliveryId).IsEqualTo(id);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private Task<DeliveryTimelineResponse?> CallHandler(DeliveryDto dto)
        => _handler.Handle(
            new GetDeliveryTimelineQuery(dto.DeliveryId),
            new FakeReadService(dto),
            CancellationToken.None);

    private static DeliveryDto MakeDto(
        Guid? id = null,
        DeliveryStatus status = DeliveryStatus.Created,
        Guid? assignedDriverId = null,
        DateTime? pickedUpAt = null,
        DateTime? deliveredAt = null,
        DateTime? cancelledAt = null,
        DateTime? expectedEta = null,
        bool anomalyFlag = false,
        AnomalyType? anomalyType = null,
        string? anomalyReason = null)
        => new()
        {
            DeliveryId       = id ?? Guid.NewGuid(),
            Status           = status,
            AssignedDriverId = assignedDriverId,
            CreatedAt        = DateTime.UtcNow.AddHours(-1),
            PickedUpAt       = pickedUpAt,
            DeliveredAt      = deliveredAt,
            CancelledAt      = cancelledAt,
            ExpectedEta      = expectedEta,
            AnomalyFlag      = anomalyFlag,
            AnomalyTypeValue = anomalyType,
            AnomalyReason    = anomalyReason,
            DistrictId       = "13433880",
        };

    private sealed class FakeReadService(DeliveryDto? dto) : IDeliveryReadService
    {
        public Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(dto);

        public Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<DeliveryDto>>([]);

        public Task<Delivery?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Delivery?>(null);

        public Task<IEnumerable<RouteWaypointDto>> GetRouteAsync(Guid deliveryId, CancellationToken ct)
            => Task.FromResult<IEnumerable<RouteWaypointDto>>([]);

        public Task<GetDeliveriesResponse> GetAllPaginatedAsync(
            string? cursor, string? status, string? districtId,
            DateTime? from, DateTime? to, int pageSize, CancellationToken ct)
            => Task.FromResult(new GetDeliveriesResponse([], null, null));
    }
}
