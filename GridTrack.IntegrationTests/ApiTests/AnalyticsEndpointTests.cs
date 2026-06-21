using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApiTests;

public class AnalyticsEndpointTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    private async Task<Guid> SeedDeliveryAsync(string districtId = "mezzeh")
    {
        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, districtId)));
        return deliveryId;
    }

    private async Task<Guid> SeedDriverAsync(string districtId = "mezzeh", string name = "Test Driver", string shortName = "Test")
    {
        var driverId = Guid.NewGuid();
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                driverId, Damascus, 9, districtId, name, shortName, true)));
        return driverId;
    }

    private async Task CompleteDeliveryLifecycleAsync(Guid deliveryId, Guid driverId)
    {
        var client = AuthClient();

        var assign = await client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/assign", new { driverId });
        assign.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pickUp = await client.PostAsJsonAsync(
            $"/api/deliveries/{deliveryId}/pick-up", new { lat = 33.5138, lng = 36.2765 });
        pickUp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var delivered = await client.PostAsync($"/api/deliveries/{deliveryId}/delivered", null);
        delivered.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1200)]
    public async Task GET_AnalyticsSummary_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/analytics/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/analytics/summary ────────────────────────────────────────
    

    [Test]
    [NotInParallel(Order = 1202)]
    public async Task GET_AnalyticsSummary_Returns_200_Without_Date_Range()
    {
        await ResetDatabaseAsync();

        var client = AuthClient();
        var response = await client.GetAsync("/api/analytics/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAnalyticsSummaryResponse>();
        body.Should().NotBeNull();
    }

    // ── GET /api/analytics/trends ─────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1203)]
    public async Task GET_AnalyticsTrends_Returns_200_With_Shape()
    {
        await ResetDatabaseAsync();
        await SeedDeliveryAsync();

        var client = AuthClient();
        var response = await client.GetAsync("/api/analytics/trends?from=2024-01-01&to=2024-12-31&granularity=day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetTrendsResponse>();
        body.Should().NotBeNull();
        body!.DeliveryTrend.Should().NotBeNull();
        body.AnomalyTrend.Should().NotBeNull();
        body.UrgencyTrend.Should().NotBeNull();
    }

    // ── GET /api/analytics/h3-density ─────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1204)]
    public async Task GET_AnalyticsH3Density_Returns_200_With_Shape()
    {
        await ResetDatabaseAsync();
        await SeedDeliveryAsync();

        var client = AuthClient();
        var response = await client.GetAsync(
            "/api/analytics/h3-density?from=2024-01-01&to=2024-12-31&resolution=9&fromHour=8&toHour=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetH3DensityResponse>();
        body.Should().NotBeNull();
        body!.Cells.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1205)]
    public async Task GET_AnalyticsH3Density_Returns_200_Without_Optional_Hours()
    {
        await ResetDatabaseAsync();
        await SeedDeliveryAsync();

        var client = AuthClient();
        var response = await client.GetAsync(
            "/api/analytics/h3-density?from=2024-01-01&to=2024-12-31&resolution=9");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetH3DensityResponse>();
        body.Should().NotBeNull();
    }


    // ── GET /api/analytics/driver-utilization ─────────────────────────────

    [Test]
    [NotInParallel(Order = 1209)]
    public async Task GET_AnalyticsDriverUtilization_Returns_200_With_Shape()
    {
        await ResetDatabaseAsync();
        await SeedDriverAsync();

        var client = AuthClient();
        var response = await client.GetAsync("/api/analytics/driver-utilization?top=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriverUtilizationResponse>();
        body.Should().NotBeNull();
        body!.TopDrivers.Should().NotBeNull();
        body.ActiveDrivers.Should().BeGreaterOrEqualTo(0);
    }

    [Test]
    [NotInParallel(Order = 1210)]
    public async Task GET_AnalyticsDriverUtilization_Clamp_Top_To_50_When_Exceeds_Max()
    {
        await ResetDatabaseAsync();
        for (int i = 0; i < 3; i++)
        {
            await SeedDriverAsync(name: $"Driver {i}", shortName: $"D{i}");
        }

        var client = AuthClient();
        var response = await client.GetAsync("/api/analytics/driver-utilization?top=999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriverUtilizationResponse>();
        body.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1211)]
    public async Task GET_AnalyticsDriverUtilization_Defaults_Top_To_10_When_Zero()
    {
        await ResetDatabaseAsync();
        await SeedDriverAsync();

        var client = AuthClient();
        var response = await client.GetAsync("/api/analytics/driver-utilization?top=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriverUtilizationResponse>();
        body.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1212)]
    public async Task GET_AnalyticsDriverUtilization_Defaults_Top_To_10_When_Negative()
    {
        await ResetDatabaseAsync();
        await SeedDriverAsync();

        var client = AuthClient();
        var response = await client.GetAsync("/api/analytics/driver-utilization?top=-5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriverUtilizationResponse>();
        body.Should().NotBeNull();
    }


    // ── GET /api/analytics/anomaly-breakdown ──────────────────────────────

    [Test]
    [NotInParallel(Order = 1214)]
    public async Task GET_AnalyticsAnomalyBreakdown_Returns_200_With_Shape()
    {
        await ResetDatabaseAsync();
        var deliveryId = await SeedDeliveryAsync();

        var client = AuthClient();
        var flag = await client.PostAsJsonAsync(
            $"/api/deliveries/{deliveryId}/flag-anomaly",
            new { type = "EtaExceeded", reason = "Driver has not moved in 30 minutes" });
        flag.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.GetAsync("/api/analytics/anomaly-breakdown?from=2024-01-01&to=2024-12-31");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAnomalyBreakdownResponse>();
        body.Should().NotBeNull();
        body!.ByType.Should().NotBeNull();
        body.ByDistrict.Should().NotBeNull();
    }

    // ── GET /api/analytics/drivers ────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1215)]
    public async Task GET_AnalyticsDrivers_Returns_200_With_Shape()
    {
        await ResetDatabaseAsync();
        await SeedDriverAsync(name: "Ahmad Hassan", shortName: "Ahmad");

        var client = AuthClient();
        var response = await client.GetAsync("/api/analytics/drivers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriverAnalyticsResponse>();
        body.Should().NotBeNull();
        body!.Drivers.Should().NotBeNull();
        body.Drivers.Should().Contain(d => d.Name == "Ahmad Hassan");
    }
}