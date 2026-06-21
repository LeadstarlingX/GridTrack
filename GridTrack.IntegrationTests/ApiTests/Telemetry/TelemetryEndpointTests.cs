using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApiTests.Telemetry;

public class TelemetryEndpointTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
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
    [NotInParallel(Order = 1600)]
    public async Task POST_Position_Returns_401_Without_Token()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(
            "/api/telemetry/position", new { driverId = Guid.NewGuid(), lat = 33.5, lng = 36.2 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/telemetry/position (write-behind) ───────────────────────

    [Test]
    [NotInParallel(Order = 1601)]
    public async Task POST_Position_Returns_204_For_Known_Driver()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var driverId = await SeedDriverAsync();

        var response = await client.PostAsJsonAsync(
            "/api/telemetry/position", new { driverId, lat = 33.5138, lng = 36.2765 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    [NotInParallel(Order = 1602)]
    public async Task POST_Position_Returns_404_For_Unknown_Driver()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PostAsJsonAsync(
            "/api/telemetry/position", new { driverId = Guid.NewGuid(), lat = 33.5138, lng = 36.2765 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/telemetry/position/sync (direct Postgres) ───────────────

    [Test]
    [NotInParallel(Order = 1603)]
    public async Task POST_PositionSync_Returns_204_For_Known_Driver()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var driverId = await SeedDriverAsync();

        var response = await client.PostAsJsonAsync(
            "/api/telemetry/position/sync", new { driverId, lat = 33.5138, lng = 36.2765 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    [NotInParallel(Order = 1604)]
    public async Task POST_PositionSync_Returns_404_For_Unknown_Driver()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.PostAsJsonAsync(
            "/api/telemetry/position/sync", new { driverId = Guid.NewGuid(), lat = 33.5138, lng = 36.2765 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/telemetry/batch ─────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1605)]
    public async Task POST_Batch_Returns_202_And_Processes_Position_Events()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();
        var driverId = await SeedDriverAsync();

        var payload = new
        {
            events = new[]
            {
                new { type = "position", driverId, lat = 33.51, lng = 36.27 },
                new { type = "position", driverId, lat = 33.52, lng = 36.28 },
            }
        };

        var response = await client.PostAsJsonAsync("/api/telemetry/batch", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<BatchResult>();
        body!.Processed.Should().Be(2);
        body.Rejected.Should().Be(0);
    }

    [Test]
    [NotInParallel(Order = 1606)]
    public async Task POST_Batch_Returns_400_For_Empty_Events()
    {
        var client = AuthClient();
        var response = await client.PostAsJsonAsync("/api/telemetry/batch", new { events = Array.Empty<object>() });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record BatchResult(int Processed, int Rejected, string[] Errors);
}
