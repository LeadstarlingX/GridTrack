using Dapper;
using FluentAssertions;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

/// <summary>
/// Verifies that the commands dispatched by TelemetryController persist correctly.
/// Tests invoke the Wolverine handlers directly (same path the controller uses).
/// </summary>
public class TelemetryHandlerIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));
    private static Point Mezzeh  => GeoFactory.CreatePoint(new Coordinate(36.2610, 33.5001));

    [Test]
    [NotInParallel(Order = 620)]
    public async Task Position_Event_Should_Update_Driver_Location_In_Database()
    {
        await ResetDatabaseAsync();

        // Seed a driver via CreateDriverCommand so the driver exists in DB
        var driverId = Guid.NewGuid();
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                DriverId: driverId,
                Location: Damascus,
                H3Resolution: 9,
                DistrictId: null,
                Name: "Khalid Test",
                ShortName: "Khalid",
                IsActive: true)));

        var newLat = 33.5001;
        var newLng = 36.2610;
        var point  = GeoFactory.CreatePoint(new Coordinate(newLng, newLat));
        var ts     = DateTime.UtcNow;

        var result = await InvokeAsync<Result>(
            new UpdateDriverPositionCommand(new UpdatePositionRequest(driverId, point, ts)));

        result.IsSuccess.Should().BeTrue();

        // Location is written by PositionFlushService (Write-Behind) up to 5 s after the command.
        var connectionFactory = Factory.Services.GetRequiredService<ISqlConnectionFactory>();
        await AssertEventuallyAsync(async () =>
        {
            using var conn = connectionFactory.CreateConnection();
            var row = await conn.QueryFirstAsync<(double X, double Y)>(
                """SELECT ST_X("Location"::geometry), ST_Y("Location"::geometry) FROM public."Drivers" WHERE "DriverId" = @Id""",
                new { Id = driverId });
            row.X.Should().BeApproximately(newLng, 0.0001);
            row.Y.Should().BeApproximately(newLat, 0.0001);
        });
    }

    [Test]
    [NotInParallel(Order = 621)]
    public async Task DeliveryCreated_Event_Should_Insert_Delivery_Row()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();
        var result = await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(
                DeliveryId: deliveryId,
                CurrentLocation: Damascus,
                H3Resolution: 7,
                ExpectedEta: DateTime.UtcNow.AddHours(2),
                DistrictId: null)));

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryId.Should().Be(deliveryId);

        var connectionFactory = Factory.Services.GetRequiredService<ISqlConnectionFactory>();
        using var conn = connectionFactory.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            """SELECT COUNT(*)::int FROM public."Deliveries" WHERE "DeliveryId" = @Id""",
            new { Id = deliveryId });

        count.Should().Be(1);
    }

    [Test]
    [NotInParallel(Order = 622)]
    public async Task DeliveryStatus_PickedUp_Should_Set_PickedUpAt_In_Database()
    {
        await ResetDatabaseAsync();

        // Create → Assign (required before PickUp) → PickUp
        var deliveryId = Guid.NewGuid();
        var driverId   = Guid.NewGuid();

        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 7, null, "test-district")));
        await InvokeAsync<Result>(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(deliveryId, driverId)));
        var pickupResult = await InvokeAsync<Result>(
            new MarkDeliveryPickedUpCommand(new PickUpDeliveryRequest(deliveryId, Mezzeh, DateTime.UtcNow)));

        pickupResult.IsSuccess.Should().BeTrue();

        var connectionFactory = Factory.Services.GetRequiredService<ISqlConnectionFactory>();
        using var conn = connectionFactory.CreateConnection();
        var pickedUpAt = await conn.ExecuteScalarAsync<DateTime?>(
            """SELECT "PickedUpAt" FROM public."Deliveries" WHERE "DeliveryId" = @Id""",
            new { Id = deliveryId });

        pickedUpAt.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 623)]
    public async Task DeliveryStatus_Delivered_Should_Set_Status_4_And_DeliveredAt()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();
        var driverId   = Guid.NewGuid();

        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 7, null, "test-district")));
        await InvokeAsync<Result>(
            new AssignDriverToDeliveryCommand(new AssignDriverRequest(deliveryId, driverId)));
        await InvokeAsync<Result>(
            new MarkDeliveryPickedUpCommand(new PickUpDeliveryRequest(deliveryId, Mezzeh, DateTime.UtcNow)));
        var deliveredResult = await InvokeAsync<Result>(
            new MarkDeliveryCompletedCommand(new CompleteDeliveryRequest(deliveryId, DateTime.UtcNow)));

        deliveredResult.IsSuccess.Should().BeTrue();

        var connectionFactory = Factory.Services.GetRequiredService<ISqlConnectionFactory>();
        using var conn = connectionFactory.CreateConnection();
        var row = await conn.QueryFirstAsync<(int Status, DateTime? DeliveredAt)>(
            """SELECT "Status", "DeliveredAt" FROM public."Deliveries" WHERE "DeliveryId" = @Id""",
            new { Id = deliveryId });

        row.Status.Should().Be(4);
        row.DeliveredAt.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 624)]
    public async Task DeliveryStatus_Cancelled_Should_Set_Status_5_And_Reason()
    {
        await ResetDatabaseAsync();

        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 7, null, "test-district")));

        var cancelResult = await InvokeAsync<Result>(
            new CancelDeliveryCommand(new CancelDeliveryRequest(
                deliveryId,
                DateTime.UtcNow,
                "Customer not home")));

        cancelResult.IsSuccess.Should().BeTrue();

        var connectionFactory = Factory.Services.GetRequiredService<ISqlConnectionFactory>();
        using var conn = connectionFactory.CreateConnection();
        var row = await conn.QueryFirstAsync<(int Status, string? Reason)>(
            """SELECT "Status", "AnomalyReason" FROM public."Deliveries" WHERE "DeliveryId" = @Id""",
            new { Id = deliveryId });

        row.Status.Should().Be(5);
        row.Reason.Should().Be("Customer not home");
    }
}
