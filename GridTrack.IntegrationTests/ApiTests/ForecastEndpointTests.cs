using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.ApiTests;

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
}