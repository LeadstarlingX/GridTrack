using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
using GridTrack.Presentation.Controllers.Deliveries;
using GridTrack.Presentation.Controllers.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApiTests;

public class EndpointContractTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    // ── GET /api/drivers ──────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1100)]
    public async Task GET_Drivers_Returns_200_With_Items_And_Cursor_Shape()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/drivers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1101)]
    public async Task GET_Drivers_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/drivers");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/deliveries ───────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1102)]
    public async Task GET_Deliveries_Returns_200()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/deliveries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    [NotInParallel(Order = 1103)]
    public async Task GET_Delivery_By_Id_Returns_404_When_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync($"/api/deliveries/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1104)]
    public async Task GET_Delivery_By_Id_Returns_200_With_Correct_Shape()
    {
        await ResetDatabaseAsync();
        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var client = AuthClient();
        var response = await client.GetAsync($"/api/deliveries/{deliveryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDeliveryByIdResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(deliveryId);
        body.DistrictId.Should().Be("mezzeh");
        body.RoutePolyline.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1105)]
    public async Task GET_Delivery_By_Id_Returns_400_For_Invalid_Guid()
    {
        var client = AuthClient();
        var response = await client.GetAsync("/api/deliveries/not-a-guid");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/drivers availability PATCH ──────────────────────────────

    [Test]
    [NotInParallel(Order = 1106)]
    public async Task PATCH_Driver_Availability_Returns_404_When_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/drivers/{Guid.NewGuid()}/availability",
            new { status = "available" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1107)]
    public async Task PATCH_Driver_Availability_Returns_200_With_Status_Field()
    {
        await ResetDatabaseAsync();
        var driverId = Guid.NewGuid();
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                driverId, Damascus, 9, "mezzeh", "Ahmad Hassan", "Ahmad", true)));

        var client = AuthClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/drivers/{driverId}/availability",
            new { status = "offline" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DriverAvailabilityResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("offline");
        body.Id.Should().Be(driverId.ToString());
    }

    // ── GET /health (anonymous) ───────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1108)]
    public async Task GET_Health_Returns_200_Without_Auth()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /api/telemetry/batch ─────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1109)]
    public async Task POST_Telemetry_Batch_Returns_400_For_Empty_Events()
    {
        var client = AuthClient();
        var response = await client.PostAsJsonAsync("/api/telemetry/batch", new { events = Array.Empty<object>() });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    [NotInParallel(Order = 1110)]
    public async Task POST_Telemetry_Batch_Returns_202_And_Counts_Processed()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var deliveryId = Guid.NewGuid();
        var payload = new
        {
            events = new[]
            {
                new
                {
                    type = "delivery_created",
                    occurredAt = DateTime.UtcNow,
                    deliveryId,
                    lat = 33.5138,
                    lng = 36.2765,
                    districtId = "mezzeh"
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/telemetry/batch", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<TelemetryBatchResult>();
        body.Should().NotBeNull();
        body!.Processed.Should().Be(1);
        body.Rejected.Should().Be(0);
    }

    // ── POST /api/deliveries/{id}/assign ─────────────────────────────────

    [Test]
    [NotInParallel(Order = 1111)]
    public async Task POST_Assign_Returns_404_When_Delivery_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{Guid.NewGuid()}/assign",
            new { driverId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1112)]
    public async Task POST_Assign_Returns_204_When_Delivery_Exists()
    {
        await ResetDatabaseAsync();
        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var client = AuthClient();
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{deliveryId}/assign",
            new { driverId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── POST /api/deliveries/{id}/cancel ─────────────────────────────────

    [Test]
    [NotInParallel(Order = 1113)]
    public async Task POST_Cancel_Returns_404_When_Delivery_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{Guid.NewGuid()}/cancel",
            new { reason = "No longer needed" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1114)]
    public async Task POST_Cancel_Returns_204_And_Delivery_Is_Cancelled()
    {
        await ResetDatabaseAsync();
        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var client = AuthClient();
        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/deliveries/{deliveryId}/cancel",
            new { reason = "Customer request" });

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/deliveries/{deliveryId}");
        var body = await getResponse.Content.ReadFromJsonAsync<GetDeliveryByIdResponse>();
        body!.Status.Should().Be("Cancelled");
    }

    // ── Full lifecycle: assign → pick-up → delivered ──────────────────────

    [Test]
    [NotInParallel(Order = 1115)]
    public async Task Full_Lifecycle_Assign_PickUp_Delivered_Via_HTTP_Endpoints()
    {
        await ResetDatabaseAsync();
        var deliveryId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var client = AuthClient();

        var assignResponse = await client.PostAsJsonAsync(
            $"/api/deliveries/{deliveryId}/assign",
            new { driverId });
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pickUpResponse = await client.PostAsJsonAsync(
            $"/api/deliveries/{deliveryId}/pick-up",
            new { lat = 33.5138, lng = 36.2765 });
        pickUpResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deliveredResponse = await client.PostAsync(
            $"/api/deliveries/{deliveryId}/delivered", null);
        deliveredResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/deliveries/{deliveryId}");
        var body = await getResponse.Content.ReadFromJsonAsync<GetDeliveryByIdResponse>();
        body!.Status.Should().Be("Delivered");
    }

    // ── POST /api/deliveries/{id}/flag-anomaly ────────────────────────────

    [Test]
    [NotInParallel(Order = 1116)]
    public async Task POST_FlagAnomaly_Returns_204_On_Valid_Delivery()
    {
        await ResetDatabaseAsync();
        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var client = AuthClient();
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{deliveryId}/flag-anomaly",
            new { type = "EtaExceeded", reason = "Driver has not moved in 30 minutes" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    [NotInParallel(Order = 1117)]
    public async Task POST_FlagAnomaly_Returns_400_For_Unknown_Type()
    {
        var client = AuthClient();
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{Guid.NewGuid()}/flag-anomaly",
            new { type = "NotARealType", reason = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/drivers ─────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1118)]
    public async Task POST_Drivers_Creates_Driver_And_Returns_201_With_Id()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/drivers", new
        {
            lat = 33.5138,
            lng = 36.2765,
            name = "Hassan Ali",
            shortName = "Hassan",
            districtId = "mezzeh",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<DriverSummaryResponse>();
        body.Should().NotBeNull();
        body!.DriverId.Should().NotBeEmpty();
        body.Name.Should().Be("Hassan Ali");
        body.DistrictId.Should().Be("mezzeh");
    }

    // ── GET /api/drivers/nearest ──────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1119)]
    public async Task GET_Drivers_Nearest_Returns_200_With_Array()
    {
        await ResetDatabaseAsync();
        var driverId = Guid.NewGuid();
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                driverId, Damascus, 9, "mezzeh", "Ali Hassan", "Ali", true)));

        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers/nearest?lat=33.5138&lng=36.2765&count=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DriverSummaryResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterOrEqualTo(1);
    }

    // ── GET /api/drivers/by-district ──────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1120)]
    public async Task GET_Drivers_ByDistrict_Returns_200_With_Matching_Drivers()
    {
        await ResetDatabaseAsync();
        var driverId = Guid.NewGuid();
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                driverId, Damascus, 9, "mezzeh", "Karim Nasser", "Karim", true)));

        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers/by-district/mezzeh");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DriverSummaryResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterOrEqualTo(1);
        body.Should().AllSatisfy(d => d.DistrictId.Should().Be("mezzeh"));
    }

    // ── GET /api/deliveries/by-district ───────────────────────────────────

    [Test]
    [NotInParallel(Order = 1121)]
    public async Task GET_Deliveries_ByDistrict_Returns_200_With_Matching_Deliveries()
    {
        await ResetDatabaseAsync();
        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var client = AuthClient();
        var response = await client.GetAsync("/api/deliveries/by-district/mezzeh");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DeliverySummaryResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterOrEqualTo(1);
        body.Should().AllSatisfy(d => d.DistrictId.Should().Be("mezzeh"));
    }

    // ── POST /api/deliveries/{id}/auto-assign ────────────────────────────

    [Test]
    [NotInParallel(Order = 1122)]
    public async Task POST_AutoAssign_Returns_404_When_Delivery_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var response = await client.PostAsync($"/api/deliveries/{Guid.NewGuid()}/auto-assign", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1123)]
    public async Task POST_AutoAssign_Returns_200_With_Candidate_List()
    {
        await ResetDatabaseAsync();
        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var client = AuthClient();
        var response = await client.PostAsync($"/api/deliveries/{deliveryId}/auto-assign", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AutoAssignContractShape>();
        body.Should().NotBeNull();
        body!.TopCandidates.Should().NotBeNull();
    }

    // ── GET /api/ai/delivery/{id}/recommendation ──────────────────────────

    [Test]
    [NotInParallel(Order = 1124)]
    public async Task GET_DeliveryRecommendation_Returns_404_When_Delivery_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var response = await client.GetAsync($"/api/ai/delivery/{Guid.NewGuid()}/recommendation");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1125)]
    public async Task GET_DeliveryRecommendation_Returns_200_With_Response_Shape()
    {
        await ResetDatabaseAsync();
        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, "mezzeh")));

        var client = AuthClient();
        var response = await client.GetAsync($"/api/ai/delivery/{deliveryId}/recommendation");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DeliveryRecommendationContractShape>();
        body.Should().NotBeNull();
        body!.DeliveryId.Should().Be(deliveryId);
        body.TopCandidates.Should().NotBeNull();
        body.AiAvailable.Should().BeFalse(); // Python unavailable in tests
    }

    // ── GET /api/ai/district-summary/{id} ────────────────────────────────

    [Test]
    [NotInParallel(Order = 1126)]
    public async Task GET_DistrictSummary_Returns_404_When_No_Data_Available()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var response = await client.GetAsync("/api/ai/district-summary/mezzeh");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Debug helper ──────────────────────────────────────────────────────

    [Test]
    public async Task Debug_Print_All_Routes()
    {
        await using var scope = Factory.Services.CreateAsyncScope();

        var endpointSources = scope.ServiceProvider
            .GetRequiredService<IEnumerable<EndpointDataSource>>();

        var list = new List<string>();
        foreach (var source in endpointSources)
        {
            foreach (var endpoint in source.Endpoints)
            {
                var routeEndpoint = endpoint as RouteEndpoint;
                list.Add($"Route: {routeEndpoint?.RoutePattern?.RawText} | Display: {endpoint.DisplayName}");
            }
        }
        list.Should().NotBeNullOrEmpty();
        await Assert.That(true).IsTrue();
    }

    private sealed record TelemetryBatchResult(int Processed, int Rejected, List<string> Errors);

    private sealed record AutoAssignContractShape(
        bool AutoAssigned,
        Guid? AssignedDriverId,
        List<object> TopCandidates);

    private sealed record DeliveryRecommendationContractShape(
        Guid DeliveryId,
        string DistrictId,
        List<object> TopCandidates,
        string? RecommendedAction,
        Guid? RecommendedDriverId,
        string? Reason,
        int? UrgencyScore,
        bool AiAvailable);
}
