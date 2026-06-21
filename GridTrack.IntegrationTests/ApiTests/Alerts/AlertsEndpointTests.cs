using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.ApiTests.Alerts;

public class AlertsEndpointTests : BaseIntegrationTest
{
    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    private static async Task<Guid> CreateDeliveryAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 36.2765, districtId = "mezzeh", expectedEta = (DateTime?)null });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<DeliveryCreatedResponse>();
        return body!.DeliveryId;
    }

    private static async Task FlagAnomalyAsync(HttpClient client, Guid deliveryId, string type)
    {
        var res = await client.PostAsJsonAsync(
            $"/api/deliveries/{deliveryId}/flag-anomaly",
            new { type, reason = "Driver has not moved in 30 minutes" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1800)]
    public async Task GET_Alerts_Returns_401_Without_Token()
    {
        var response = await Factory.CreateClient().GetAsync("/api/alerts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/alerts ───────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1801)]
    public async Task GET_Alerts_Returns_200_With_Empty_Items_When_No_Anomalies()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/alerts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAlertsResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 1802)]
    public async Task GET_Alerts_Returns_Flagged_Anomaly()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var deliveryId = await CreateDeliveryAsync(client);
        await FlagAnomalyAsync(client, deliveryId, "EtaExceeded");

        var response = await client.GetAsync("/api/alerts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAlertsResponse>();
        body!.Items.Should().Contain(i => i.DeliveryId == deliveryId && i.AnomalyType == "EtaExceeded");
    }

    [Test]
    [NotInParallel(Order = 1803)]
    public async Task GET_Alerts_Filters_By_AnomalyType()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var d1 = await CreateDeliveryAsync(client);
        var d2 = await CreateDeliveryAsync(client);
        await FlagAnomalyAsync(client, d1, "EtaExceeded");
        await FlagAnomalyAsync(client, d2, "RouteDeviation");

        var response = await client.GetAsync("/api/alerts?anomalyType=EtaExceeded");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAlertsResponse>();
        body!.Items.Should().OnlyContain(i => i.AnomalyType == "EtaExceeded");
    }
}
