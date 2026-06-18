using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class AutoAssignIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static Point Damascus => Geo.CreatePoint(new Coordinate(36.2765, 33.5138));
    private static Point Aleppo  => Geo.CreatePoint(new Coordinate(37.1612, 36.2021));

    private static Driver MakeDriver(Point location, string district = "mezzeh")
    {
        var r = Driver.Create(Guid.NewGuid(), location, district, DateTime.UtcNow,
            "Test Driver", "TD", isActive: true);
        r.IsSuccess.Should().BeTrue();
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    private static Delivery MakeDelivery(Point origin)
    {
        var d = Delivery.Create(Guid.NewGuid(), origin, "mezzeh", DateTime.UtcNow, null).Value;
        d.ClearDomainEvents();
        return d;
    }

    [Test]
    [NotInParallel(Order = 400)]
    public async Task AutoAssign_Returns_404_When_Delivery_Not_Found()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<Result<AutoAssignResponse>>(
            new AutoAssignDeliveryCommand(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
    }

    [Test]
    [NotInParallel(Order = 401)]
    public async Task AutoAssign_Returns_Candidates_Without_Assigning_When_No_Drivers()
    {
        await ResetDatabaseAsync();

        var delivery = MakeDelivery(Damascus);
        await SeedDeliveriesAsync([delivery]);

        var result = await InvokeAsync<Result<AutoAssignResponse>>(
            new AutoAssignDeliveryCommand(delivery.DeliveryId));

        result.IsSuccess.Should().BeTrue();
        result.Value.AutoAssigned.Should().BeFalse();
        result.Value.AssignedDriverId.Should().BeNull();
        result.Value.TopCandidates.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 402)]
    public async Task AutoAssign_Directly_Assigns_When_Single_Candidate_Available()
    {
        await ResetDatabaseAsync();

        var driver   = MakeDriver(Damascus);
        var delivery = MakeDelivery(Damascus);
        await SeedDriversAsync([driver]);
        await SeedDeliveriesAsync([delivery]);

        var result = await InvokeAsync<Result<AutoAssignResponse>>(
            new AutoAssignDeliveryCommand(delivery.DeliveryId));

        result.IsSuccess.Should().BeTrue();
        result.Value.AutoAssigned.Should().BeTrue();
        result.Value.AssignedDriverId.Should().Be(driver.DriverId);
        result.Value.TopCandidates.Should().HaveCount(1);
        result.Value.TopCandidates[0].DriverId.Should().Be(driver.DriverId);
    }

    [Test]
    [NotInParallel(Order = 403)]
    public async Task AutoAssign_Returns_Top3_Candidates_Without_Assigning_When_Gap_Is_Unclear()
    {
        await ResetDatabaseAsync();

        // Two drivers at exactly the same location → scores will be nearly identical.
        // Gap will be below AutoAssignGapThreshold (0.15) so human confirmation needed.
        var driverA  = MakeDriver(Damascus);
        var driverB  = MakeDriver(Damascus);
        var delivery = MakeDelivery(Damascus);
        await SeedDriversAsync([driverA, driverB]);
        await SeedDeliveriesAsync([delivery]);

        var result = await InvokeAsync<Result<AutoAssignResponse>>(
            new AutoAssignDeliveryCommand(delivery.DeliveryId));

        result.IsSuccess.Should().BeTrue();
        result.Value.AutoAssigned.Should().BeFalse();
        result.Value.AssignedDriverId.Should().BeNull();
        // Both candidates returned (capped at 3)
        result.Value.TopCandidates.Should().HaveCountGreaterThanOrEqualTo(2);
        // Candidates include both drivers
        result.Value.TopCandidates.Select(c => c.DriverId)
            .Should().Contain(driverA.DriverId)
            .And.Contain(driverB.DriverId);
    }
}
