using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Analytics;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class HeatmapHandlerIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static Delivery CreateDelivery(
        string districtId = "h3-heatmap",
        DateTime? createdAt = null)
    {
        var r = Delivery.Create(Guid.NewGuid(), Damascus, districtId, createdAt ?? DateTime.UtcNow, null);
        r.IsSuccess.Should().BeTrue();
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    [Test]
    [NotInParallel(Order = 400)]
    public async Task GetHeatmapDataQuery_Returns_Success_With_Empty_Points_When_No_Deliveries()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<Result<IEnumerable<HeatmapPointDto>>>(
            new GetHeatmapDataQuery(new HeatmapRequest("h3-heatmap", DateTime.UtcNow.AddHours(-1))));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 401)]
    public async Task GetHeatmapDataQuery_Returns_Points_For_District()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([
            CreateDelivery(districtId: "h3-heatmap-a"),
            CreateDelivery(districtId: "h3-heatmap-a"),
            CreateDelivery(districtId: "h3-heatmap-b"),
        ]);

        var result = await InvokeAsync<Result<IEnumerable<HeatmapPointDto>>>(
            new GetHeatmapDataQuery(new HeatmapRequest("h3-heatmap-a", DateTime.UtcNow.AddHours(-1))));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Test]
    [NotInParallel(Order = 402)]
    public async Task GetHeatmapDataQuery_Respects_Window_Filter()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        await SeedDeliveriesAsync([
            CreateDelivery(districtId: "h3-heatmap-window", createdAt: now.AddHours(-3)),
            CreateDelivery(districtId: "h3-heatmap-window", createdAt: now.AddMinutes(-30)),
        ]);

        var result = await InvokeAsync<Result<IEnumerable<HeatmapPointDto>>>(
            new GetHeatmapDataQuery(new HeatmapRequest("h3-heatmap-window", now.AddHours(-1))));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Test]
    [NotInParallel(Order = 403)]
    public async Task GetHeatmapDataQuery_Returns_Correct_Coordinates()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([CreateDelivery(districtId: "h3-heatmap-coords")]);

        var result = await InvokeAsync<Result<IEnumerable<HeatmapPointDto>>>(
            new GetHeatmapDataQuery(new HeatmapRequest("h3-heatmap-coords", DateTime.UtcNow.AddHours(-1))));

        result.IsSuccess.Should().BeTrue();
        var point = result.Value.Single();
        ((double)point.Latitude).Should().BeApproximately(33.5138, 0.001);
        ((double)point.Longitude).Should().BeApproximately(36.2765, 0.001);
    }

    [Test]
    [NotInParallel(Order = 404)]
    public async Task GetHeatmapDataQuery_Returns_Intensity_10_For_Created_Status()
    {
        await ResetDatabaseAsync();

        var delivery = CreateDelivery(districtId: "h3-heatmap-intensity");
        await SeedDeliveriesAsync([delivery]);

        var result = await InvokeAsync<Result<IEnumerable<HeatmapPointDto>>>(
            new GetHeatmapDataQuery(new HeatmapRequest("h3-heatmap-intensity", DateTime.UtcNow.AddHours(-1))));

        result.IsSuccess.Should().BeTrue();
        result.Value.Single().Intensity.Should().Be(1.0);
    }
}
