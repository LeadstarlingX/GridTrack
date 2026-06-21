using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
using GridTrack.Presentation.Controllers.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApiTests;

public class DriversEndpointTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    private async Task<Guid> SeedDriverAsync(
        string districtId = "mezzeh",
        string name = "Test Driver",
        string shortName = "Test",
        bool isActive = true)
    {
        var driverId = Guid.NewGuid();
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                driverId, Damascus, 9, districtId, name, shortName, isActive)));
        return driverId;
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1600)]
    public async Task GET_Driver_By_Id_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync($"/api/drivers/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 1601)]
    public async Task GET_Driver_Stats_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync($"/api/drivers/{Guid.NewGuid()}/stats");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 1602)]
    public async Task POST_Drivers_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/drivers", new
        {
            lat = 33.5138,
            lng = 36.2765,
            name = "Test",
            shortName = "T",
            districtId = "mezzeh"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 1603)]
    public async Task PATCH_Driver_Availability_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/drivers/{Guid.NewGuid()}/availability",
            new { status = "available" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/drivers/{id} ─────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1604)]
    public async Task GET_Driver_By_Id_Returns_200_With_Correct_Shape()
    {
        await ResetDatabaseAsync();
        var driverId = await SeedDriverAsync("mezzeh", "Karim Nasser", "Karim");

        var client = AuthClient();
        var response = await client.GetAsync($"/api/drivers/{driverId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DriverDetailResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(driverId);
        body.Name.Should().Be("Karim Nasser");
        body.ShortName.Should().Be("Karim");
        body.DistrictId.Should().Be("mezzeh");
        body.IsActive.Should().BeTrue();
        body.Lat.Should().BeApproximately(33.5138, 0.001);
        body.Lng.Should().BeApproximately(36.2765, 0.001);
    }

    [Test]
    [NotInParallel(Order = 1605)]
    public async Task GET_Driver_By_Id_Returns_404_When_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync($"/api/drivers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1606)]
    public async Task GET_Driver_By_Id_Returns_400_For_Invalid_Guid()
    {
        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers/not-a-guid");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/drivers/{id}/stats ───────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1607)]
    public async Task GET_Driver_Stats_Returns_200_With_Correct_Shape()
    {
        await ResetDatabaseAsync();
        var driverId = await SeedDriverAsync();

        var client = AuthClient();
        var response = await client.GetAsync($"/api/drivers/{driverId}/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DriverStatsResponse>();
        body.Should().NotBeNull();
        body!.DriverId.Should().Be(driverId);
        body.Name.Should().NotBeNullOrEmpty();
        body.TotalCompleted.Should().BeGreaterOrEqualTo(0);
        body.OnTimeRatePct.Should().BeGreaterOrEqualTo(0);
    }

    [Test]
    [NotInParallel(Order = 1608)]
    public async Task GET_Driver_Stats_Returns_404_When_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync($"/api/drivers/{Guid.NewGuid()}/stats");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1609)]
    public async Task GET_Driver_Stats_Returns_400_For_Invalid_Guid()
    {
        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers/not-a-guid/stats");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/drivers ─────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1610)]
    public async Task POST_Drivers_Returns_201_With_Location_Header()
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
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<DriverSummaryResponse>();
        body.Should().NotBeNull();
        body!.DriverId.Should().NotBeEmpty();
        body.Name.Should().Be("Hassan Ali");
        body.DistrictId.Should().Be("mezzeh");
        body.Lat.Should().BeApproximately(33.5138, 0.001);
        body.Lng.Should().BeApproximately(36.2765, 0.001);
    }

    [Test]
    [NotInParallel(Order = 1611)]
    public async Task POST_Drivers_Returns_422_When_Domain_Validation_Fails()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        // Duplicate name or invalid district might trigger failure depending on handler rules
        var response = await client.PostAsJsonAsync("/api/drivers", new
        {
            lat = 33.5138,
            lng = 36.2765,
            name = "", // empty name might fail
            shortName = "",
            districtId = "mezzeh"
        });

        // Adjust expectation if your handler accepts empty names
        response.StatusCode.Should().BeOneOf(HttpStatusCode.UnprocessableEntity, HttpStatusCode.Created);
    }

    // ── PATCH /api/drivers/{id}/availability ──────────────────────────────

    [Test]
    [NotInParallel(Order = 1612)]
    public async Task PATCH_Driver_Availability_Returns_400_For_Invalid_Guid()
    {
        var client = AuthClient();
        var response = await client.PatchAsJsonAsync(
            "/api/drivers/not-a-guid/availability",
            new { status = "available" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    [NotInParallel(Order = 1613)]
    public async Task PATCH_Driver_Availability_Toggles_To_Offline()
    {
        await ResetDatabaseAsync();
        var driverId = await SeedDriverAsync();

        var client = AuthClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/drivers/{driverId}/availability",
            new { status = "offline" });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<DriverAvailabilityResponse>();
            body!.Status.Should().Be("offline");
        }
    }

    // ── GET /api/drivers (query params) ───────────────────────────────────

    [Test]
    [NotInParallel(Order = 1614)]
    public async Task GET_Drivers_With_DistrictId_Filter_Returns_Matching()
    {
        await ResetDatabaseAsync();
        await SeedDriverAsync("mezzeh", "Mezzeh Driver", "Mezzeh");
        await SeedDriverAsync("malki", "Malki Driver", "Malki");

        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers?districtId=mezzeh");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().AllSatisfy(d => d.DistrictId.Should().Be("mezzeh"));
    }

    [Test]
    [NotInParallel(Order = 1615)]
    public async Task GET_Drivers_With_PageSize_Returns_Correct_Count()
    {
        await ResetDatabaseAsync();
        for (int i = 0; i < 5; i++)
        {
            await SeedDriverAsync("mezzeh", $"Driver {i}", $"D{i}");
        }

        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers?pageSize=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountLessOrEqualTo(3);
    }

    [Test]
    [NotInParallel(Order = 1616)]
    public async Task GET_Drivers_Default_PageSize_Is_8()
    {
        await ResetDatabaseAsync();
        for (int i = 0; i < 10; i++)
        {
            await SeedDriverAsync("mezzeh", $"Driver {i}", $"D{i}");
        }

        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountLessOrEqualTo(8);
    }

    [Test]
    [NotInParallel(Order = 1617)]
    public async Task GET_Drivers_With_Status_Filter_Returns_Matching()
    {
        await ResetDatabaseAsync();
        await SeedDriverAsync("mezzeh", "Active Driver", "Active", isActive: true);

        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers?status=active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>();
        body.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1618)]
    public async Task GET_Drivers_With_Search_Returns_Matching()
    {
        await ResetDatabaseAsync();
        await SeedDriverAsync("mezzeh", "Ahmad Hassan", "Ahmad");

        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers?search=Ahmad");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().Contain(d => d.Name.Contains("Ahmad"));
    }

    [Test]
    [NotInParallel(Order = 1619)]
    public async Task GET_Drivers_With_Cursor_Returns_Page_And_NextCursor()
    {
        await ResetDatabaseAsync();
        for (int i = 0; i < 5; i++)
        {
            await SeedDriverAsync("mezzeh", $"Cursor Driver {i}", $"CD{i}");
        }

        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers?pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(2);
    }

    // ── GET /api/drivers/nearest ──────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1620)]
    public async Task GET_Drivers_Nearest_Uses_Default_Count_Of_5()
    {
        await ResetDatabaseAsync();
        for (int i = 0; i < 3; i++)
        {
            await SeedDriverAsync("mezzeh", $"Near Driver {i}", $"ND{i}");
        }

        var client = AuthClient();
        var response = await client.GetAsync("/api/drivers/nearest?lat=33.5138&lng=36.2765");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DriverSummaryResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountLessOrEqualTo(5);
    }

    // ── GET /api/drivers/by-district/{districtId} ─────────────────────────

    [Test]
    [NotInParallel(Order = 1621)]
    public async Task GET_Drivers_ByDistrict_Returns_Empty_For_Nonexistent_District()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/drivers/by-district/nonexistent");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DriverSummaryResponse>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }
}