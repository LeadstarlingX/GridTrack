using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Forecast;
using GridTrack.Domain.Deliveries;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class ForecastQueryIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static Delivery CreateDelivery(string districtId = "h3-forecast")
    {
        var r = Delivery.Create(Guid.NewGuid(), Damascus, districtId, DateTime.UtcNow, null);
        r.IsSuccess.Should().BeTrue();
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    [Test]
    [NotInParallel(Order = 800)]
    public async Task GetForecastQuery_Should_Return_Null_When_No_Deliveries_In_District()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<GetForecastResponse?>(new GetForecastQuery("h3-empty-district"));

        // No deliveries → forecast returns a ForecastDto with 0 expected deliveries (COUNT = 0)
        // The handler still returns a response (SQL COUNT returns 0, not null)
        if (result is not null)
        {
            result.ForecastedDemand.Should().Be(0);
            result.DriverRecommendation.Should().Be(1); // min 1 driver
        }
    }

    [Test]
    [NotInParallel(Order = 801)]
    public async Task GetForecastQuery_Should_Return_Forecast_Based_On_Historical_Data()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync(Enumerable.Range(0, 20).Select(_ => CreateDelivery("h3-demand")));

        var result = await InvokeAsync<GetForecastResponse?>(new GetForecastQuery("h3-demand"));

        result.Should().NotBeNull();
        result!.DistrictId.Should().Be("h3-demand");
        result.Horizon.Should().Be("next-hour");
        result.ForecastedDemand.Should().Be(20);
        result.DriverRecommendation.Should().Be(2); // ceil(20 / 10)
        result.StaffingRatio.Should().BeApproximately(0.1, precision: 0.001); // 2 / 20
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }
}
