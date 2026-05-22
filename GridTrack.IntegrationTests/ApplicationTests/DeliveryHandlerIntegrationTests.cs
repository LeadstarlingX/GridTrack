using FluentAssertions;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Common;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.ValueObjects;
using GridTrack.Infrastructure.Data;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class DeliveryHandlerIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);

    private static Point Damascus  => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));
    private static Point NearPoint => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5230));

    [Test]
    [NotInParallel(Order = 200)]
    public async Task CreateDeliveryCommand_Should_Create_Delivery_And_Return_Response()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();
        var result = await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(
                DeliveryId: deliveryId,
                CurrentLocation: Damascus,
                H3Resolution: 9,
                ExpectedEta: DateTime.UtcNow.AddHours(1),
                DistrictId: null)));

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryId.Should().Be(deliveryId);
        result.Value.DistrictId.Should().NotBeNullOrEmpty();
    }

    [Test]
    [NotInParallel(Order = 201)]
    public async Task UpdateDeliveryLocationCommand_Should_Update_Location_And_Publish_Events()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();
        var driverId   = Guid.NewGuid();

        // Created → Assigned → PickedUp (required before UpdateLocation)
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "h3-test-district")));
        await InvokeAsync<Result>(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(deliveryId, driverId)));
        await InvokeAsync<Result>(
            new MarkDeliveryPickedUpCommand(new PickUpDeliveryRequest(deliveryId, Damascus, DateTime.UtcNow)));

        var result = await InvokeAsync<Result>(
            new UpdateDeliveryLocationCommand(new UpdateLocationRequest(deliveryId, NearPoint, DateTime.UtcNow)));

        result.IsSuccess.Should().BeTrue();

        var delivery = await ResolveAsync<IDeliveryReadService, DeliveryDto?>(
            rs => rs.GetByIdAsync(deliveryId, CancellationToken.None));

        delivery.Should().NotBeNull();
        delivery!.CurrentLocation.Coordinate.X.Should().BeApproximately(NearPoint.X, precision: 0.0001);
        delivery.CurrentLocation.Coordinate.Y.Should().BeApproximately(NearPoint.Y, precision: 0.0001);
    }

    [Test]
    [NotInParallel(Order = 202)]
    public async Task UpdateDeliveryLocationCommand_Should_Return_Failure_When_Delivery_Not_Found()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<Result>(
            new UpdateDeliveryLocationCommand(new UpdateLocationRequest(Guid.NewGuid(), Damascus, DateTime.UtcNow)));

        result.IsFailure.Should().BeTrue();
    }

    [Test]
    [NotInParallel(Order = 203)]
    public async Task AssignDriverToDeliveryCommand_Should_Assign_Driver_And_Publish_Events()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();
        var driverId   = Guid.NewGuid();

        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "h3-test-district")));

        var result = await InvokeAsync<Result>(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(deliveryId, driverId)));

        result.IsSuccess.Should().BeTrue();

        var delivery = await ResolveAsync<IDeliveryReadService, DeliveryDto?>(
            rs => rs.GetByIdAsync(deliveryId, CancellationToken.None));

        delivery.Should().NotBeNull();
        delivery!.AssignedDriverId.Should().Be(driverId);
    }

    [Test]
    [NotInParallel(Order = 204)]
    public async Task AssignDriverToDeliveryCommand_Should_Return_Failure_When_Delivery_Not_Found()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<Result>(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(Guid.NewGuid(), Guid.NewGuid())));

        result.IsFailure.Should().BeTrue();
    }

    [Test]
    [NotInParallel(Order = 205)]
    public async Task MarkDeliveryCompletedCommand_Should_Mark_As_Delivered_And_Publish_Events()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();
        var driverId   = Guid.NewGuid();

        // Created → Assigned → PickedUp (required before MarkDelivered)
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "h3-test-district")));
        await InvokeAsync<Result>(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(deliveryId, driverId)));
        await InvokeAsync<Result>(
            new MarkDeliveryPickedUpCommand(new PickUpDeliveryRequest(deliveryId, Damascus, DateTime.UtcNow)));

        var result = await InvokeAsync<Result>(
            new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(deliveryId, DateTime.UtcNow)));

        result.IsSuccess.Should().BeTrue();

        var delivery = await ResolveAsync<IDeliveryReadService, DeliveryDto?>(
            rs => rs.GetByIdAsync(deliveryId, CancellationToken.None));

        delivery.Should().NotBeNull();
        delivery!.Status.Should().Be(DeliveryStatus.Delivered);
    }

    [Test]
    [NotInParallel(Order = 206)]
    public async Task CancelDeliveryCommand_Should_Cancel_Delivery_And_Publish_Events()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();

        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "h3-test-district")));

        var result = await InvokeAsync<Result>(
            new CancelDeliveryCommand(new CancelDeliveryRequest(deliveryId, DateTime.UtcNow, "Customer requested cancellation")));

        result.IsSuccess.Should().BeTrue();

        var delivery = await ResolveAsync<IDeliveryReadService, DeliveryDto?>(
            rs => rs.GetByIdAsync(deliveryId, CancellationToken.None));

        delivery.Should().NotBeNull();
        delivery!.Status.Should().Be(DeliveryStatus.Cancelled);
    }

    [Test]
    [NotInParallel(Order = 207)]
    public async Task FlagDeliveryAnomalyCommand_Should_Flag_Anomaly_And_Publish_Events()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();

        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "h3-test-district")));

        var result = await InvokeAsync<Result>(
            new FlagDeliveryAnomalyCommand(new FlagAnomalyRequest(
                deliveryId,
                Domain.ValueObjects.AnomalyType.EtaExceeded,
                "Delivery is significantly delayed")));

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    [NotInParallel(Order = 208)]
    public async Task GetDeliveryByIdQuery_Should_Return_Delivery_Details()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();

        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "h3-test-district")));

        var response = await InvokeAsync<GetDeliveryByIdResponse?>(new GetDeliveryByIdQuery(deliveryId));

        response.Should().NotBeNull();
        response!.Id.Should().Be(deliveryId);
        response.DistrictId.Should().Be("h3-test-district");
    }

    [Test]
    [NotInParallel(Order = 209)]
    public async Task GetDeliveryByIdQuery_Should_Return_Null_When_Not_Found()
    {
        await ResetDatabaseAsync();

        var response = await InvokeAsync<GetDeliveryByIdResponse?>(new GetDeliveryByIdQuery(Guid.NewGuid()));

        response.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 210)]
    public async Task GetDeliveriesByDistrictQuery_Should_Return_Deliveries_In_District()
    {
        await ResetDatabaseAsync();

        var delivery1Id = Guid.NewGuid();
        var delivery2Id = Guid.NewGuid();

        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(delivery1Id, Damascus, 9, null, "h3-district-1")));
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(delivery2Id, NearPoint, 9, null, "h3-district-2")));

        var result = await InvokeAsync<Result<IEnumerable<DeliveryDto>>>(
            new GetDeliveriesByDistrictQuery(new DistrictFilterRequest("h3-district-1")));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().DeliveryId.Should().Be(delivery1Id);
    }

    [Test]
    [NotInParallel(Order = 211)]
    public async Task GetDeliveryRouteQuery_Returns_Empty_When_No_Waypoints()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var result = await InvokeAsync<IEnumerable<RouteWaypointDto>>(
            new GetDeliveryRouteQuery(deliveryId));

        result.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 212)]
    public async Task GetDeliveryRouteQuery_Returns_Waypoints_When_Route_Seeded()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var routes = new[]
        {
            new DeliveryRoute { DeliveryId = deliveryId, Sequence = 1, Lat = 33.51, Lng = 36.27 },
            new DeliveryRoute { DeliveryId = deliveryId, Sequence = 2, Lat = 33.52, Lng = 36.28 },
        };
        await SeedDeliveryRoutesAsync(routes);

        var result = (await InvokeAsync<IEnumerable<RouteWaypointDto>>(
            new GetDeliveryRouteQuery(deliveryId))).ToList();

        result.Should().HaveCount(2);
        result[0].Lat.Should().BeApproximately(33.51, 0.0001);
        result[1].Lat.Should().BeApproximately(33.52, 0.0001);
    }
}
