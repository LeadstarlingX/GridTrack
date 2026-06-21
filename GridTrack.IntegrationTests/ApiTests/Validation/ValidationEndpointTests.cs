using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.ApiTests.Validation;

/// <summary>
/// Exercises the FluentValidation → ValidationException → 400 path (previously unreachable:
/// no validators existed). Each test sends an authenticated request with an invalid body and
/// asserts a 400 whose payload names the offending field.
/// </summary>
public class ValidationEndpointTests : BaseIntegrationTest
{
    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    // ── POST /api/deliveries ──────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1400)]
    public async Task POST_CreateDelivery_With_OutOfRange_Lat_Returns_400()
    {
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 999.0, lng = 36.2765, districtId = (string?)null, expectedEta = (DateTime?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ValidationFailure");
        body.Should().Contain("Lat");
    }

    [Test]
    [NotInParallel(Order = 1401)]
    public async Task POST_CreateDelivery_With_OutOfRange_Lng_Returns_400()
    {
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 500.0, districtId = (string?)null, expectedEta = (DateTime?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Lng");
    }

    // ── POST /api/district-groups ─────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1402)]
    public async Task POST_CreateDistrictGroup_With_Empty_Name_Returns_400()
    {
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/district-groups",
            new { name = "", districtIds = new[] { "mezzeh" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Name");
    }

    [Test]
    [NotInParallel(Order = 1403)]
    public async Task POST_CreateDistrictGroup_With_No_Districts_Returns_400()
    {
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/district-groups",
            new { name = "Central", districtIds = Array.Empty<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("DistrictIds");
    }

    // ── POST /api/deliveries/{id}/cancel ──────────────────────────────────

    [Test]
    [NotInParallel(Order = 1404)]
    public async Task POST_CancelDelivery_With_Empty_Reason_Returns_400()
    {
        var client = AuthClient();

        // Validation runs before the handler, so a well-formed (but non-existent) id still 400s.
        var response = await client.PostAsJsonAsync(
            $"/api/deliveries/{Guid.NewGuid()}/cancel", new { reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Reason");
    }

    // ── A valid body still passes (filter doesn't false-positive) ─────────

    [Test]
    [NotInParallel(Order = 1405)]
    public async Task POST_CreateDelivery_With_Valid_Coordinates_Does_Not_400()
    {
        var client = AuthClient();

        var response = await client.PostAsJsonAsync("/api/deliveries",
            new { lat = 33.5138, lng = 36.2765, districtId = "mezzeh", expectedEta = (DateTime?)null });

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }
}
