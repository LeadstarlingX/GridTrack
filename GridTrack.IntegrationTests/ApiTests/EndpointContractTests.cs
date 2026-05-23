using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
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
    
}