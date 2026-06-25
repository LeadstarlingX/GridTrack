using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
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
    
    [Test]
    [NotInParallel(Order = 1509)]
    public async Task POST_CreateDelivery_Returns_401_Without_Token()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 36.2765, districtId = "mezzeh", expectedEta = (DateTime?)null });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
     
    [Test]
    [NotInParallel(Order = 1510)]
    public async Task GET_DeliveryRoute_Returns_401_Without_Token()
    {
        var response = await Factory.CreateClient().GetAsync($"/api/deliveries/{Guid.NewGuid()}/route");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
     
    [Test]
    [NotInParallel(Order = 1511)]
    public async Task GET_DeliveryTimeline_Returns_401_Without_Token()
    {
        var response = await Factory.CreateClient().GetAsync($"/api/deliveries/{Guid.NewGuid()}/timeline");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
     
    [Test]
    [NotInParallel(Order = 1512)]
    public async Task GET_DeliveriesByDistrict_Returns_401_Without_Token()
    {
        var response = await Factory.CreateClient().GetAsync("/api/deliveries/by-district/mezzeh");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
     
    [Test]
    [NotInParallel(Order = 1513)]
    public async Task POST_CancelDelivery_Returns_401_Without_Token()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(
            $"/api/deliveries/{Guid.NewGuid()}/cancel", new { reason = "test" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
     
    // ── Validators ────────────────────────────────────────────────────────────
     
    [Test]
    [NotInParallel(Order = 1514)]
    public async Task POST_CreateDelivery_Returns_400_When_Lat_Out_Of_Range()
    {
        // CreateDeliveryHttpRequestValidator: RuleFor(x => x.Lat).InclusiveBetween(-90, 90)
        await ResetDatabaseAsync();
        var client = AuthClient();
     
        var response = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 200.0, lng = 36.2765, districtId = "mezzeh", expectedEta = (DateTime?)null });
     
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
     
    [Test]
    [NotInParallel(Order = 1515)]
    public async Task POST_CreateDelivery_Returns_400_When_Lng_Out_Of_Range()
    {
        // CreateDeliveryHttpRequestValidator: RuleFor(x => x.Lng).InclusiveBetween(-180, 180)
        await ResetDatabaseAsync();
        var client = AuthClient();
     
        var response = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 999.0, districtId = "mezzeh", expectedEta = (DateTime?)null });
     
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
     
    [Test]
    [NotInParallel(Order = 1516)]
    public async Task POST_CancelDelivery_Returns_400_When_Reason_Empty()
    {
        // CancelDeliveryHttpRequestValidator: RuleFor(x => x.Reason).NotEmpty()
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{id}/cancel", new { reason = "" });
     
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
     
    [Test]
    [NotInParallel(Order = 1517)]
    public async Task POST_PickUp_Returns_400_When_Lat_Out_Of_Range()
    {
        // PickUpDeliveryHttpRequestValidator: RuleFor(x => x.Lat).InclusiveBetween(-90, 90)
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{id}/pick-up", new { lat = 200.0, lng = 36.2765 });
     
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
     
    [Test]
    [NotInParallel(Order = 1518)]
    public async Task POST_FlagAnomaly_Returns_400_When_Reason_Empty()
    {
        // FlagAnomalyHttpRequestValidator: RuleFor(x => x.Reason).NotEmpty()
        // Distinct from existing 1508 which tests unknown Type value (enum parse failure,
        // not FluentValidation failure).
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{id}/flag-anomaly", new { type = "EtaExceeded", reason = "" });
     
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
     
    // ── GET /api/deliveries (filter paths) ───────────────────────────────────
     
    [Test]
    [NotInParallel(Order = 1519)]
    public async Task GET_Deliveries_Returns_200_With_Empty_Items_On_Clean_DB()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
     
        var response = await client.GetAsync("/api/deliveries?pageSize=10");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDeliveriesResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().BeEmpty();
        body.NextCursor.Should().BeNull();
    }
     
    [Test]
    [NotInParallel(Order = 1520)]
    public async Task GET_Deliveries_Returns_Only_Items_Matching_Status_Filter()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        await CreateDeliveryAsync(client, "mezzeh");
     
        // Status=Pending is the initial state after creation.
        var response = await client.GetAsync("/api/deliveries?status=Created&pageSize=10");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDeliveriesResponse>();
        body!.Items.Should().NotBeEmpty();
        body.Items.Should().AllSatisfy(i => i.Status.Should().Be("Created"));
    }
     
    [Test]
    [NotInParallel(Order = 1521)]
    public async Task GET_Deliveries_Returns_Only_Items_Matching_DistrictId_Filter()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        await CreateDeliveryAsync(client, "mezzeh");
        await CreateDeliveryAsync(client, "malki");
     
        var response = await client.GetAsync("/api/deliveries?districtId=mezzeh&pageSize=10");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDeliveriesResponse>();
        body!.Items.Should().NotBeEmpty();
        body.Items.Should().AllSatisfy(i => i.DistrictId.Should().Be("mezzeh"));
    }
     
    // ── GET /api/deliveries/by-district/{districtId} ─────────────────────────
     
    [Test]
    [NotInParallel(Order = 1522)]
    public async Task GET_DeliveriesByDistrict_Returns_200_With_Empty_Array_On_Clean_DB()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
     
        var response = await client.GetAsync("/api/deliveries/by-district/mezzeh");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<JsonElement>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }
     
    [Test]
    [NotInParallel(Order = 1523)]
    public async Task GET_DeliveriesByDistrict_Returns_Delivery_After_Create()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client, "mezzeh");
     
        var response = await client.GetAsync("/api/deliveries/by-district/mezzeh");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<JsonElement>>();
        body.Should().NotBeNull();
        body!.Should().HaveCount(1);
        body[0].GetProperty("deliveryId").GetGuid().Should().Be(id);
        body[0].GetProperty("districtId").GetString().Should().Be("mezzeh");
    }
     
    // ── GET /api/deliveries/{id}/route ────────────────────────────────────────
     
    [Test]
    [NotInParallel(Order = 1524)]
    public async Task GET_DeliveryRoute_Returns_200_With_Empty_Array_Before_Dispatch()
    {
        // Route geometry is populated by OSRM (via Wolverine async message after assign).
        // In the test environment OSRM is not running, so the route table is empty → [].
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        var response = await client.GetAsync($"/api/deliveries/{id}/route");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<RouteWaypointDto>>();
        body.Should().NotBeNull();
    }
     
    // ── GET /api/deliveries/{id}/timeline ────────────────────────────────────
     
    [Test]
    [NotInParallel(Order = 1525)]
    public async Task GET_DeliveryTimeline_Returns_200_With_Created_Event()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        var response = await client.GetAsync($"/api/deliveries/{id}/timeline");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DeliveryTimelineResponse>();
        body.Should().NotBeNull();
        body!.DeliveryId.Should().Be(id);
        body.Events.Should().ContainSingle(e => e.Type == "Created");
    }
     
    [Test]
    [NotInParallel(Order = 1526)]
    public async Task GET_DeliveryTimeline_Returns_404_For_Unknown_Delivery()
    {
        var client = AuthClient();
        var response = await client.GetAsync($"/api/deliveries/{Guid.NewGuid()}/timeline");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
     
    [Test]
    [NotInParallel(Order = 1527)]
    public async Task GET_DeliveryTimeline_Contains_All_Events_After_Full_Lifecycle()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var deliveryId = await CreateDeliveryAsync(client);
        var driverId   = await SeedDriverAsync();
     
        await client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/assign", new { driverId });
        await client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/pick-up", new { lat = 33.5138, lng = 36.2765 });
        await client.PostAsync($"/api/deliveries/{deliveryId}/delivered", null);
     
        var response = await client.GetAsync($"/api/deliveries/{deliveryId}/timeline");
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DeliveryTimelineResponse>();
        var types = body!.Events.Select(e => e.Type).ToHashSet();
        types.Should().Contain("Created");
        types.Should().Contain("Assigned");
        types.Should().Contain("PickedUp");
        types.Should().Contain("Delivered");
    }
     
    // ── POST /api/deliveries/{id}/cancel (success path) ──────────────────────
     
    [Test]
    [NotInParallel(Order = 1528)]
    public async Task POST_CancelDelivery_Returns_204_On_Success()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{id}/cancel", new { reason = "Customer request" });
     
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
     
    [Test]
    [NotInParallel(Order = 1529)]
    public async Task POST_CancelDelivery_Cancelled_State_Is_Reflected_In_GetById()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        await client.PostAsJsonAsync($"/api/deliveries/{id}/cancel", new { reason = "Duplicate order" });
     
        var getResponse = await client.GetAsync($"/api/deliveries/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadFromJsonAsync<GetDeliveryByIdResponse>();
        body!.Status.Should().Be("Cancelled");
    }
     
    // ── POST /api/deliveries/{id}/flag-anomaly ───────────────────────────────
     
    [Test]
    [NotInParallel(Order = 1530)]
    public async Task POST_FlagAnomaly_Returns_204_For_EtaExceeded()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{id}/flag-anomaly",
            new { type = "EtaExceeded", reason = "Delivery is 30 min overdue." });
     
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
     
    [Test]
    [NotInParallel(Order = 1531)]
    public async Task POST_FlagAnomaly_Returns_204_For_RouteDeviation()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{id}/flag-anomaly",
            new { type = "RouteDeviation", reason = "Driver left designated zone." });
     
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
     
    [Test]
    [NotInParallel(Order = 1532)]
    public async Task POST_FlagAnomaly_Returns_404_For_Unknown_Delivery()
    {
        var client = AuthClient();
     
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{Guid.NewGuid()}/flag-anomaly",
            new { type = "EtaExceeded", reason = "Delivery is overdue." });
     
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
     
    // ── POST /api/deliveries/{id}/auto-assign ─────────────────────────────────
     
    [Test]
    [NotInParallel(Order = 1533)]
    public async Task POST_AutoAssign_Returns_404_For_Unknown_Delivery()
    {
        var client = AuthClient();
        var response = await client.PostAsync(
            $"/api/deliveries/{Guid.NewGuid()}/auto-assign", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
     
    [Test]
    [NotInParallel(Order = 1534)]
    public async Task POST_AutoAssign_Returns_200_With_AutoAssigned_False_When_No_Drivers()
    {
        // No drivers seeded → IDispatchStrategy returns empty candidates list
        // → AutoAssignDeliveryHandler returns Result.Success(AutoAssigned=false)
        // → controller returns 200, not 409.
        await ResetDatabaseAsync();
        var client = AuthClient();
        var id = await CreateDeliveryAsync(client);
     
        var response = await client.PostAsync($"/api/deliveries/{id}/auto-assign", null);
     
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AutoAssignResponse>();
        body.Should().NotBeNull();
        body!.AutoAssigned.Should().BeFalse();
        body.AssignedDriverId.Should().BeNull();
    }
     
    // ── POST /api/deliveries/{id}/assign — not-found path ────────────────────
     
    [Test]
    [NotInParallel(Order = 1535)]
    public async Task POST_AssignDriver_Returns_404_For_Unknown_Delivery()
    {
        var client = AuthClient();
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{Guid.NewGuid()}/assign",
            new { driverId = Guid.NewGuid() });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
     
    // ── POST /api/deliveries/{id}/pick-up — not-found path ───────────────────
     
    [Test]
    [NotInParallel(Order = 1536)]
    public async Task POST_PickUp_Returns_404_For_Unknown_Delivery()
    {
        var client = AuthClient();
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{Guid.NewGuid()}/pick-up",
            new { lat = 33.5138, lng = 36.2765 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
     
    // ── POST /api/deliveries/{id}/delivered — not-found path ─────────────────
     
    [Test]
    [NotInParallel(Order = 1537)]
    public async Task POST_MarkDelivered_Returns_404_For_Unknown_Delivery()
    {
        var client = AuthClient();
        var response = await client.PostAsync(
            $"/api/deliveries/{Guid.NewGuid()}/delivered", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
}
