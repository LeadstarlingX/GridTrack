using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.ApiTests.Forecast;

public class ForecastEndpointTests : BaseIntegrationTest
{
    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1500)]
    public async Task GET_Forecast_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/forecast/mezzeh");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 1501)]
    public async Task GET_Staffing_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/forecast/staffing?districtId=mezzeh&targetAt=2026-06-15T09:00:00Z");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/forecast/{districtId} ────────────────────────────────────


    [Test]
    [NotInParallel(Order = 1504)]
    public async Task GET_Forecast_Returns_200_For_Different_Districts()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/forecast/malki");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetForecastResponse>();
        body.Should().NotBeNull();
        body!.DistrictId.Should().Be("malki");
    }

    // ── GET /api/forecast/staffing ────────────────────────────────────────


    [Test]
    [NotInParallel(Order = 1506)]
    public async Task GET_Staffing_Returns_400_When_DistrictId_Missing()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync(
            "/api/forecast/staffing?targetAt=2026-06-15T09:00:00Z");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    [NotInParallel(Order = 1507)]
    public async Task GET_Staffing_Returns_400_When_DistrictId_Empty()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync(
            "/api/forecast/staffing?districtId=&targetAt=2026-06-15T09:00:00Z");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    [NotInParallel(Order = 1508)]
    public async Task GET_Staffing_Returns_400_When_DistrictId_Whitespace()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync(
            "/api/forecast/staffing?districtId=%20%20&targetAt=2026-06-15T09:00:00Z");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Test]
    [NotInParallel(Order = 1510)]
    public async Task GET_Staffing_Returns_200_For_Different_Target_Times()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync(
            "/api/forecast/staffing?districtId=mezzeh&targetAt=2026-06-15T18:00:00Z");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    [NotInParallel(Order = 1511)]
    public async Task GET_Staffing_Returns_200_For_Different_Districts()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync(
            "/api/forecast/staffing?districtId=malki&targetAt=2026-06-15T09:00:00Z");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }
    
    [Test]
    [NotInParallel(Order = 1502)]
    public async Task GET_Forecast_Returns_200_With_Validated_Shape()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
     
        var response = await client.GetAsync("/api/forecast/mezzeh");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetForecastResponse>();
        body.Should().NotBeNull();
        body!.DistrictId.Should().Be("mezzeh");
        body.Horizon.Should().Be("next-hour");
        body.DriverRecommendation.Should().BeGreaterOrEqualTo(1); // max(1, ceil(n/10))
        body.ForecastedDemand.Should().BeGreaterOrEqualTo(0);
        body.StaffingRatio.Should().BeGreaterOrEqualTo(0);
    }
     
    [Test]
    [NotInParallel(Order = 1503)]
    public async Task GET_Forecast_Reflects_Deliveries_Created_In_Last_Hour()
    {
        // ForecastReadService queries COUNT(*) WHERE CreatedAt >= UtcNow-1h.
        // Seeding two deliveries for mezzeh must push ForecastedDemand to at least 2.
        await ResetDatabaseAsync();
        var client = AuthClient();
     
        await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 36.2765, districtId = "mezzeh", expectedEta = (DateTime?)null });
        await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 36.2765, districtId = "mezzeh", expectedEta = (DateTime?)null });
     
        var response = await client.GetAsync("/api/forecast/mezzeh");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetForecastResponse>();
        body!.ForecastedDemand.Should().BeGreaterOrEqualTo(2);
        // DriverRecommendation = max(1, ceil(ForecastedDemand / 10))
        body.DriverRecommendation.Should().BeGreaterOrEqualTo(1);
    }
     
    // ── GET /api/forecast/staffing (gaps at 1505, 1509) ──────────────────────
     
    [Test]
    [NotInParallel(Order = 1505)]
    public async Task GET_Staffing_Returns_200_Or_503_For_Current_Timestamp()
    {
        // PythonForecastService targets localhost:8000 which is not running in test
        // environment, so the expected result is 503. BeOneOf guards against either
        // the service being available in some CI environment.
        await ResetDatabaseAsync();
        var client = AuthClient();
     
        var targetAt = DateTime.UtcNow.ToString("O");
        var response = await client.GetAsync(
            $"/api/forecast/staffing?districtId=mezzeh&targetAt={Uri.EscapeDataString(targetAt)}");
     
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
     
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            body.GetProperty("code").GetString().Should().Be("AI_UNAVAILABLE");
        }
    }
     
    [Test]
    [NotInParallel(Order = 1509)]
    public async Task GET_Staffing_Returns_200_Or_503_For_Past_Date()
    {
        // Verifies the handler does not short-circuit on historical targetAt values.
        // GetStaffingForecastHandler has no past-date guard — it calls analytics
        // for dayOfWeek/hour from the given timestamp, then forwards to the AI service.
        await ResetDatabaseAsync();
        var client = AuthClient();
     
        var pastDate = DateTime.UtcNow.AddDays(-30).ToString("O");
        var response = await client.GetAsync(
            $"/api/forecast/staffing?districtId=mezzeh&targetAt={Uri.EscapeDataString(pastDate)}");
     
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
    }
    
}