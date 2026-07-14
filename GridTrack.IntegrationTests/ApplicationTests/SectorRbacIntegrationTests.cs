using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class SectorRbacIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static Point AnyPoint => Geo.CreatePoint(new Coordinate(36.2765, 33.5138));

    // Districts controlled by the test — never overlap with other test suites.
    private const string AlphaDistrict = "rbac-test-alpha";
    private const string BetaDistrict  = "rbac-test-beta";

    private static HttpClient ClientAs(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> PostDeliveryAsync(HttpClient client, string districtId)
    {
        var res = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 36.2765, districtId, expectedEta = (DateTime?)null });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<DeliveryCreatedResponse>();
        return body!.DeliveryId;
    }

    // ── 900: Observer sees only their district ────────────────────────────

    [Test]
    [NotInParallel(Order = 900)]
    public async Task Observer_GetDeliveries_Returns_Only_Allowed_Districts()
    {
        await ResetDatabaseAsync();

        var adminClient    = ClientAs(TestAuthHandler.GeneralObserverToken);
        var alphaId        = await PostDeliveryAsync(adminClient, AlphaDistrict);
        await PostDeliveryAsync(adminClient, BetaDistrict);

        var observerClient = ClientAs($"{TestAuthHandler.ObserverPrefix}{AlphaDistrict}");
        var response       = await observerClient.GetAsync("/api/deliveries?pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDeliveriesResponse>();
        body!.Items.Should().HaveCount(1);
        body.Items[0].Id.Should().Be(alphaId);
    }

    // ── 901: GeneralObserver sees all districts ───────────────────────────

    [Test]
    [NotInParallel(Order = 901)]
    public async Task GeneralObserver_GetDeliveries_Returns_All_Districts()
    {
        await ResetDatabaseAsync();

        var client = ClientAs(TestAuthHandler.GeneralObserverToken);
        await PostDeliveryAsync(client, AlphaDistrict);
        await PostDeliveryAsync(client, BetaDistrict);

        var response = await client.GetAsync("/api/deliveries?pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDeliveriesResponse>();
        body!.Items.Should().HaveCount(2);
    }

    // ── 902: Observer getting a delivery outside their sector → 404 ───────

    [Test]
    [NotInParallel(Order = 902)]
    public async Task Observer_GetDeliveryById_OutsideSector_Returns_404()
    {
        await ResetDatabaseAsync();

        var adminClient  = ClientAs(TestAuthHandler.GeneralObserverToken);
        var betaId       = await PostDeliveryAsync(adminClient, BetaDistrict);

        var observerClient = ClientAs($"{TestAuthHandler.ObserverPrefix}{AlphaDistrict}");
        var response       = await observerClient.GetAsync($"/api/deliveries/{betaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── 903: Observer with no sectors gets zero results ───────────────────

    [Test]
    [NotInParallel(Order = 903)]
    public async Task Observer_WithNoSectors_GetDeliveries_Returns_Empty()
    {
        await ResetDatabaseAsync();

        var adminClient = ClientAs(TestAuthHandler.GeneralObserverToken);
        await PostDeliveryAsync(adminClient, AlphaDistrict);

        // ObserverPrefix with no district IDs after the pipe → empty allowed list
        var noSectorClient = ClientAs(TestAuthHandler.ObserverPrefix);
        var response       = await noSectorClient.GetAsync("/api/deliveries?pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDeliveriesResponse>();
        body!.Items.Should().BeEmpty();
    }

    // ── 904: Unauthenticated → 401 ────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 904)]
    public async Task NoToken_GetDeliveries_Returns_401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/deliveries");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 905: Observer multi-district token works correctly ────────────────

    [Test]
    [NotInParallel(Order = 905)]
    public async Task Observer_WithTwoDistricts_Sees_Both()
    {
        await ResetDatabaseAsync();

        var adminClient = ClientAs(TestAuthHandler.GeneralObserverToken);
        await PostDeliveryAsync(adminClient, AlphaDistrict);
        await PostDeliveryAsync(adminClient, BetaDistrict);

        var twoDistrictClient =
            ClientAs($"{TestAuthHandler.ObserverPrefix}{AlphaDistrict}|{BetaDistrict}");
        var response = await twoDistrictClient.GetAsync("/api/deliveries?pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDeliveriesResponse>();
        body!.Items.Should().HaveCount(2);
    }
}