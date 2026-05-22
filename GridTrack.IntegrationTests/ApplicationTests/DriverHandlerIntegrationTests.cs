using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Common;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class DriverHandlerIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);

    private static Point Damascus  => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));
    private static Point NearPoint => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5230));
    private static Point Aleppo    => GeoFactory.CreatePoint(new Coordinate(37.1611, 36.2021));

    [Test]
    [NotInParallel(Order = 100)]
    public async Task CreateDriverCommand_Should_Create_Driver_And_Return_Dto()
    {
        await ResetDatabaseAsync();

        var driverId = Guid.NewGuid();
        var result = await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                DriverId: driverId,
                Location: Damascus,
                H3Resolution: 9,
                DistrictId: null,
                IsActive: true)));

        result.IsSuccess.Should().BeTrue();
        result.Value.DriverId.Should().Be(driverId);
        result.Value.Location.Should().NotBeNull();
        result.Value.IsActive.Should().BeTrue();
    }

    [Test]
    [NotInParallel(Order = 103)]
    public async Task UpdateDriverPositionCommand_Should_Return_Failure_When_Driver_Not_Found()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<Result>(
            new UpdateDriverPositionCommand(new UpdatePositionRequest(Guid.NewGuid(), Damascus, DateTime.UtcNow)));

        result.IsFailure.Should().BeTrue();
    }

    [Test]
    [NotInParallel(Order = 104)]
    public async Task ToggleDriverAvailabilityCommand_Should_Return_Null_When_Driver_Not_Found()
    {
        await ResetDatabaseAsync();

        var response = await InvokeAsync<DriverAvailabilityResponse?>(
            new ToggleDriverAvailabilityCommand(Guid.NewGuid(), false));

        response.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 105)]
    public async Task GetNearestDriversQuery_Should_Return_Nearest_Active_Drivers()
    {
        await ResetDatabaseAsync();

        var driver1Id = Guid.NewGuid();
        var driver2Id = Guid.NewGuid();
        var driver3Id = Guid.NewGuid();

        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(driver1Id, NearPoint, 9, "h3-near", true)));
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(driver2Id, Aleppo, 9, "h3-far", true)));
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(driver3Id, Damascus, 9, "h3-center", true)));

        var result = await InvokeAsync<Result<IEnumerable<DriverDto>>>(
            new GetNearestDriversQuery(new NearestDriversRequest(Damascus, 2)));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.First().DriverId.Should().Be(driver3Id);
    }

    [Test]
    [NotInParallel(Order = 106)]
    public async Task GetDriversByDistrictQuery_Should_Return_Drivers_In_District()
    {
        await ResetDatabaseAsync();

        var district1DriverId = Guid.NewGuid();
        var district2DriverId = Guid.NewGuid();

        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(district1DriverId, Damascus, 9, "h3-district-1", true)));
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(district2DriverId, Aleppo, 9, "h3-district-2", true)));

        var result = await InvokeAsync<Result<IEnumerable<DriverDto>>>(
            new GetDriversByDistrictQuery(new DistrictFilterRequest("h3-district-1")));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().DriverId.Should().Be(district1DriverId);
    }
}
