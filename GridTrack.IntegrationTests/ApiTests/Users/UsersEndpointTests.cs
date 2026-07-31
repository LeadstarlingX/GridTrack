using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Domain.Authentication;
using GridTrack.Domain.DistrictGroups;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.ApiTests.Users;

public class UsersEndpointTests : BaseIntegrationTest
{
    private static HttpClient AdminClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.GeneralObserverToken}");
        return client;
    }

    private static HttpClient ObserverClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ObserverPrefix}mezzeh");
        return client;
    }

    private static Task<AppUser> SeedObserverAsync(Guid[]? sectors = null)
    {
        var user = AppUser.Create(Guid.NewGuid(), $"obs_{Guid.NewGuid():N}", "hash", "Observer", sectors ?? []).Value;
        return SeedAsync(ctx => { ctx.Set<AppUser>().Add(user); return Task.CompletedTask; })
               .ContinueWith(_ => user);
    }

    private static Task<DistrictGroup> SeedGroupAsync()
    {
        var group = DistrictGroup.Create(Guid.NewGuid(), "Test Group", ["d1"]).Value;
        return SeedAsync(ctx => { ctx.Set<DistrictGroup>().Add(group); return Task.CompletedTask; })
               .ContinueWith(_ => group);
    }

    // ── Authorization ─────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1400)]
    public async Task GET_Users_Returns_401_Without_Token()
    {
        var client   = Factory.CreateClient();
        var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 1401)]
    public async Task GET_Users_Returns_403_For_Observer_Role()
    {
        var client   = ObserverClient();
        var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    [NotInParallel(Order = 1402)]
    public async Task PATCH_Users_Sectors_Returns_403_For_Observer_Role()
    {
        var client   = ObserverClient();
        var response = await client.PatchAsJsonAsync($"/api/users/{Guid.NewGuid()}/sectors", new { sectorIds = Array.Empty<Guid>() });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/users ────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1403)]
    public async Task GET_Users_Returns_200_With_Array()
    {
        await ResetDatabaseAsync();
        await SeedObserverAsync();

        var client   = AdminClient();
        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThan(0);
    }

    // ── PATCH /api/users/{id}/sectors ─────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1404)]
    public async Task PATCH_Users_Sectors_Returns_404_When_User_Not_Found()
    {
        await ResetDatabaseAsync();
        var client   = AdminClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/users/{Guid.NewGuid()}/sectors",
            new { sectorIds = Array.Empty<Guid>() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    [NotInParallel(Order = 1405)]
    public async Task PATCH_Users_Sectors_Returns_200_With_Updated_Dto()
    {
        await ResetDatabaseAsync();
        var user  = await SeedObserverAsync();
        var group = await SeedGroupAsync();

        var client   = AdminClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/users/{user.UserId}/sectors",
            new { sectorIds = new[] { group.Id } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserDto>();
        body.Should().NotBeNull();
        body!.SectorIds.Should().Contain(group.Id);
    }

    [Test]
    [NotInParallel(Order = 1406)]
    public async Task PATCH_Users_Sectors_Returns_200_With_Empty_Array_Revoking_Access()
    {
        await ResetDatabaseAsync();
        var group = await SeedGroupAsync();
        var user  = await SeedObserverAsync([group.Id]);

        var client   = AdminClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/users/{user.UserId}/sectors",
            new { sectorIds = Array.Empty<Guid>() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserDto>();
        body!.SectorIds.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 1407)]
    public async Task PATCH_Users_Sectors_Returns_422_For_Unknown_SectorId()
    {
        await ResetDatabaseAsync();
        var user = await SeedObserverAsync();

        var client   = AdminClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/users/{user.UserId}/sectors",
            new { sectorIds = new[] { Guid.NewGuid() } });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    [NotInParallel(Order = 1408)]
    public async Task PATCH_Users_Sectors_Returns_400_For_Duplicate_SectorIds()
    {
        await ResetDatabaseAsync();
        var user = await SeedObserverAsync();
        var id   = Guid.NewGuid();

        var client   = AdminClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/users/{user.UserId}/sectors",
            new { sectorIds = new[] { id, id } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    [NotInParallel(Order = 1409)]
    public async Task PATCH_Users_Sectors_Returns_400_For_GeneralObserver_Target()
    {
        await ResetDatabaseAsync();
        var admin = AppUser.Create(Guid.NewGuid(), $"admin_{Guid.NewGuid():N}", "hash", "GeneralObserver", []).Value;
        await SeedAsync(ctx => { ctx.Set<AppUser>().Add(admin); return Task.CompletedTask; });

        var client   = AdminClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/users/{admin.UserId}/sectors",
            new { sectorIds = Array.Empty<Guid>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Cascade cleanup ───────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1410)]
    public async Task DELETE_DistrictGroup_Removes_It_From_User_SectorIds()
    {
        await ResetDatabaseAsync();
        var group = await SeedGroupAsync();
        var user  = await SeedObserverAsync([group.Id]);

        var client         = AdminClient();
        var deleteResponse = await client.DeleteAsync($"/api/district-groups/{group.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The user's sectors should now be empty.
        var getResponse = await client.GetAsync("/api/users");
        var users       = await getResponse.Content.ReadFromJsonAsync<List<UserDto>>();
        var updated     = users!.Single(u => u.UserId == user.UserId);
        updated.SectorIds.Should().BeEmpty();
    }
}
