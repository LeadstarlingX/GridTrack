using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.DistrictGroups;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApiTests;

public class DistrictGroupsEndpointTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1300)]
    public async Task GET_DistrictGroups_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/district-groups");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 1301)]
    public async Task POST_DistrictGroups_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/district-groups", new { name = "test", districtIds = Array.Empty<string>() });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/district-groups ──────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1302)]
    public async Task GET_DistrictGroups_Returns_200_With_Array()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/district-groups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DistrictGroupDto>>();
        body.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1303)]
    public async Task GET_DistrictGroups_Returns_200_With_Seeded_Item()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var create = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "West Districts",
            districtIds = new[] { "mezzeh", "malki" }
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync("/api/district-groups");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<DistrictGroupDto>>();
        body.Should().NotBeNull();
        body!.Should().Contain(d => d.Name == "West Districts");
    }

    // ── GET /api/district-groups/{id} ─────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1304)]
    public async Task GET_DistrictGroup_By_Id_Returns_404_When_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync($"/api/district-groups/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1305)]
    public async Task GET_DistrictGroup_By_Id_Returns_200_With_Correct_Shape()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var createResponse = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "Central Districts",
            districtIds = new[] { "mezzeh" }
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<DistrictGroupDto>();

        var response = await client.GetAsync($"/api/district-groups/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DistrictGroupDto>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.Name.Should().Be("Central Districts");
        body.DistrictIds.Should().Contain("mezzeh");
    }

    // ── POST /api/district-groups ─────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1306)]
    public async Task POST_DistrictGroup_Returns_201_With_Id_And_Location()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "East Districts",
            districtIds = new[] { "bab_touma", "masaken_barzeh" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<DistrictGroupDto>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Name.Should().Be("East Districts");
        body.DistrictIds.Should().HaveCount(2);
    }

    [Test]
    [NotInParallel(Order = 1307)]
    public async Task POST_DistrictGroup_Returns_422_For_Duplicate_Name()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var first = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "Unique Group",
            districtIds = new[] { "mezzeh" }
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "Unique Group",
            districtIds = new[] { "malki" }
        });

        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    [NotInParallel(Order = 1308)]
    public async Task POST_DistrictGroup_Returns_422_For_Empty_Name()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "",
            districtIds = new[] { "mezzeh" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── PUT /api/district-groups/{id} ─────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1309)]
    public async Task PUT_DistrictGroup_Returns_404_When_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PutAsJsonAsync(
            $"/api/district-groups/{Guid.NewGuid()}",
            new { name = "Updated", districtIds = new[] { "mezzeh" } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1310)]
    public async Task PUT_DistrictGroup_Returns_204_And_Updates_Data()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var createResponse = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "Old Name",
            districtIds = new[] { "mezzeh" }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<DistrictGroupDto>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/district-groups/{created!.Id}",
            new { name = "New Name", districtIds = new[] { "mezzeh", "malki" } });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/district-groups/{created.Id}");
        var body = await getResponse.Content.ReadFromJsonAsync<DistrictGroupDto>();
        body!.Name.Should().Be("New Name");
        body.DistrictIds.Should().Contain("malki");
    }

    [Test]
    [NotInParallel(Order = 1311)]
    public async Task PUT_DistrictGroup_Returns_422_For_Duplicate_Name()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var first = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "Group A",
            districtIds = new[] { "mezzeh" }
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "Group B",
            districtIds = new[] { "malki" }
        });
        var secondBody = await second.Content.ReadFromJsonAsync<DistrictGroupDto>();

        var update = await client.PutAsJsonAsync(
            $"/api/district-groups/{secondBody!.Id}",
            new { name = "Group A", districtIds = new[] { "malki" } });

        update.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── DELETE /api/district-groups/{id} ──────────────────────────────────

    [Test]
    [NotInParallel(Order = 1312)]
    public async Task DELETE_DistrictGroup_Returns_404_When_Not_Found()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.DeleteAsync($"/api/district-groups/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1313)]
    public async Task DELETE_DistrictGroup_Returns_204_And_Removes_Item()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var createResponse = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "To Delete",
            districtIds = new[] { "mezzeh" }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<DistrictGroupDto>();

        var deleteResponse = await client.DeleteAsync($"/api/district-groups/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/district-groups/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Full CRUD lifecycle ───────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1314)]
    public async Task Full_Lifecycle_Create_Read_Update_Delete()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/district-groups", new
        {
            name = "Lifecycle Group",
            districtIds = new[] { "district_1", "district_2" }
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<DistrictGroupDto>();

        // Read
        var getResponse = await client.GetAsync($"/api/district-groups/{created!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Update
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/district-groups/{created.Id}",
            new { name = "Updated Lifecycle Group", districtIds = new[] { "district_1", "district_2", "district_3" } });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify update
        var getUpdated = await client.GetAsync($"/api/district-groups/{created.Id}");
        var updatedBody = await getUpdated.Content.ReadFromJsonAsync<DistrictGroupDto>();
        updatedBody!.Name.Should().Be("Updated Lifecycle Group");
        updatedBody.DistrictIds.Should().HaveCount(3);

        // Delete
        var deleteResponse = await client.DeleteAsync($"/api/district-groups/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var getDeleted = await client.GetAsync($"/api/district-groups/{created.Id}");
        getDeleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}