using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.ApiTests.Export;

public class ExportEndpointTests : BaseIntegrationTest
{
    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    private static async Task CreateDeliveryAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 36.2765, districtId = "mezzeh", expectedEta = (DateTime?)null });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1700)]
    public async Task POST_ExportCsv_Returns_401_Without_Token()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(
            "/api/export/csv", new { mode = "deliveries" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/export/csv ──────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1701)]
    public async Task POST_ExportCsv_Returns_Csv_With_Header_When_Empty()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/export/csv", new { mode = "deliveries" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("DeliveryId");
    }

    [Test]
    [NotInParallel(Order = 1702)]
    public async Task POST_ExportCsv_Includes_Seeded_Deliveries()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        await CreateDeliveryAsync(client);
        await CreateDeliveryAsync(client);

        var response = await client.PostAsJsonAsync("/api/export/csv", new { mode = "deliveries" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var csv = await response.Content.ReadAsStringAsync();
        var rows = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        rows.Should().HaveCountGreaterThan(1); // header + at least one data row
    }
}
