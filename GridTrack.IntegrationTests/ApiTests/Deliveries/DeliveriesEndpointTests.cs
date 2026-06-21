using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApiTests.Deliveries;

public class DeliveriesEndpointTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    private static async Task<Guid> CreateDeliveryAsync(HttpClient client, string districtId = "mezzeh")
    {
        var res = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 36.2765, districtId, expectedEta = (DateTime?)null });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<DeliveryCreatedResponse>();
        return body!.DeliveryId;
    }

    private async Task<Guid> SeedDriverAsync(string districtId = "mezzeh")
    {
        var driverId = Guid.NewGuid();
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                driverId, Damascus, 9, districtId, "Test Driver", "Test", true)));
        return driverId;
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1500)]
    public async Task GET_Deliveries_Returns_401_Without_Token()
    {
        var response = await Factory.CreateClient().GetAsync("/api/deliveries");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/deliveries ───────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1501)]
    public async Task GET_Deliveries_Returns_200_With_Shape()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        await CreateDeliveryAsync(client);

        var response = await client.GetAsync("/api/deliveries?pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDeliveriesResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeNull();
        body.Items.Should().HaveCountGreaterThan(0);
    }

    // ── POST /api/deliveries ──────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1502)]
    public async Task POST_CreateDelivery_Returns_201_With_Location()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 36.2765, districtId = "mezzeh", expectedEta = (DateTime?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var body = await response.Content.ReadFromJsonAsync<DeliveryCreatedResponse>();
        body!.DeliveryId.Should().NotBe(Guid.Empty);
    }

    // ── GET /api/deliveries/{id} ──────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1503)]
    public async Task GET_DeliveryById_Returns_200_After_Create()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);

        var response = await client.GetAsync($"/api/deliveries/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    [NotInParallel(Order = 1504)]
    public async Task GET_DeliveryById_Returns_404_For_Unknown()
    {
        var client = AuthClient();
        var response = await client.GetAsync($"/api/deliveries/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1505)]
    public async Task GET_DeliveryById_Returns_400_For_Non_Guid()
    {
        var client = AuthClient();
        var response = await client.GetAsync("/api/deliveries/not-a-guid");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Lifecycle: create → assign → pick-up → delivered ──────────────────

    [Test]
    [NotInParallel(Order = 1506)]
    public async Task Delivery_Lifecycle_Assign_PickUp_Delivered_All_204()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var deliveryId = await CreateDeliveryAsync(client);
        var driverId = await SeedDriverAsync();

        var assign = await client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/assign", new { driverId });
        assign.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pickUp = await client.PostAsJsonAsync(
            $"/api/deliveries/{deliveryId}/pick-up", new { lat = 33.5138, lng = 36.2765 });
        pickUp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var delivered = await client.PostAsync($"/api/deliveries/{deliveryId}/delivered", null);
        delivered.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Not-found / conflict on commands ──────────────────────────────────

    [Test]
    [NotInParallel(Order = 1507)]
    public async Task POST_CancelDelivery_Returns_404_For_Unknown_Delivery()
    {
        var client = AuthClient();
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{Guid.NewGuid()}/cancel", new { reason = "no longer needed" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── flag-anomaly unknown type → controller-level 400 ──────────────────

    [Test]
    [NotInParallel(Order = 1508)]
    public async Task POST_FlagAnomaly_With_Unknown_Type_Returns_400()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{id}/flag-anomaly", new { type = "NotARealType", reason = "x reason here" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
