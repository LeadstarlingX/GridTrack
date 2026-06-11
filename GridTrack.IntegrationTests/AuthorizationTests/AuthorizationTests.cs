using System.Net;
using FluentAssertions;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.AuthorizationTests;

public class AuthorizationTests : BaseIntegrationTest
{
    // ── Unauthenticated access ─────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 800)]
    public async Task ProtectedEndpoint_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/drivers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 801)]
    public async Task ProtectedEndpoint_Returns_401_With_Malformed_Token()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer this.is.not.a.valid.jwt");

        var response = await client.GetAsync("/api/drivers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 802)]
    public async Task ProtectedEndpoint_Returns_401_With_Empty_Bearer()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer ");

        var response = await client.GetAsync("/api/drivers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Authenticated access ───────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 803)]
    public async Task ProtectedEndpoint_Returns_200_With_Valid_Token()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");

        var response = await client.GetAsync("/api/drivers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Anonymous routes ───────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 804)]
    public async Task HealthEndpoint_Returns_200_Without_Token()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── CORS ───────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 805)]
    public async Task Preflight_With_Allowed_Origin_Returns_CORS_Headers()
    {
        var client = Factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/drivers");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization");

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).Should().BeTrue();
        origins!.Should().Contain("http://localhost:5173");
    }

    [Test]
    [NotInParallel(Order = 806)]
    public async Task Preflight_With_Disallowed_Origin_Does_Not_Return_CORS_Header()
    {
        var client = Factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/drivers");
        request.Headers.Add("Origin", "http://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Test]
    [NotInParallel(Order = 807)]
    public async Task Credentialed_Request_From_Allowed_Origin_Receives_AllowCredentials_Header()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");

        var response = await client.GetAsync("/api/drivers");

        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var vals).Should().BeTrue();
        vals!.Should().Contain("true");
    }
}
