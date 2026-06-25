using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApiTests.Districts;

public class DistrictsEndpointTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static HttpClient AuthClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuthHandler.ValidToken}");
        return client;
    }

    private async Task<Guid> SeedDeliveryAsync(string districtId = "mezzeh", DateTime? createdAt = null)
    {
        var deliveryId = Guid.NewGuid();
        await InvokeAsync<Result<DeliveryCreatedResponse>>(
            new CreateDeliveryCommand(new CreateDeliveryRequest(deliveryId, Damascus, 9, null, districtId)));

        // If a specific CreatedAt is needed, we assume the handler sets it to UtcNow.
        // For sparkline tests we rely on the delivery being created within the query window.
        return deliveryId;
    }

    private sealed record SparklinePoint(DateTime Hour, int Count);

    // ── Auth ──────────────────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1400)]
    public async Task GET_Districts_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/districts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 1401)]
    public async Task GET_DistrictBoundaries_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/districts/boundaries");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    [NotInParallel(Order = 1402)]
    public async Task GET_DistrictSparkline_Returns_401_Without_Token()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/districts/mezzeh/sparkline");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/districts ────────────────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1403)]
    public async Task GET_Districts_Returns_200_With_Array()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/districts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DistrictItemResponse>>();
        body.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1404)]
    public async Task GET_Districts_Returns_Items_With_Centroid_Shape()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/districts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<DistrictItemResponse>>();
        body.Should().NotBeNull();
        body!.Should().AllSatisfy(d =>
        {
            d.Id.Should().NotBeNullOrEmpty();
            d.Name.Should().NotBeNullOrEmpty();
            d.Centroid.Should().NotBeNull();
        });
    }

    // ── GET /api/districts/boundaries ─────────────────────────────────────

    [Test]
    [NotInParallel(Order = 1405)]
    public async Task GET_DistrictBoundaries_Returns_200_With_GeoJson_Shape()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/districts/boundaries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDistrictBoundariesResponse>();
        body.Should().NotBeNull();
        body!.Type.Should().Be("FeatureCollection");
        body.Features.Should().NotBeNull();
    }
    

    // ── GET /api/districts/{districtId}/sparkline ────────────────────────

    [Test]
    [NotInParallel(Order = 1407)]
    public async Task GET_DistrictSparkline_Returns_200_With_Data_For_Existing_Deliveries()
    {
        await ResetDatabaseAsync();
        await SeedDeliveryAsync("mezzeh");

        var client = AuthClient();
        var response = await client.GetAsync("/api/districts/mezzeh/sparkline?hours=6");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SparklinePoint>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterOrEqualTo(1);
        body.Should().AllSatisfy(p =>
        {
            p.Hour.Should().BeOnOrBefore(DateTime.UtcNow);
            p.Count.Should().BeGreaterOrEqualTo(1);
        });
    }

    [Test]
    [NotInParallel(Order = 1408)]
    public async Task GET_DistrictSparkline_Returns_200_With_Empty_Array_When_No_Data()
    {
        await ResetDatabaseAsync();
        var client = AuthClient();

        var response = await client.GetAsync("/api/districts/nonexistent-district/sparkline?hours=6");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SparklinePoint>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 1409)]
    public async Task GET_DistrictSparkline_Uses_Default_Hours_When_Not_Provided()
    {
        await ResetDatabaseAsync();
        await SeedDeliveryAsync("mezzeh");

        var client = AuthClient();
        var response = await client.GetAsync("/api/districts/mezzeh/sparkline");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SparklinePoint>>();
        body.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1410)]
    public async Task GET_DistrictSparkline_Respects_Custom_Hours_Parameter()
    {
        await ResetDatabaseAsync();
        await SeedDeliveryAsync("mezzeh");

        var client = AuthClient();
        var response = await client.GetAsync("/api/districts/mezzeh/sparkline?hours=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SparklinePoint>>();
        body.Should().NotBeNull();
    }

    [Test]
    [NotInParallel(Order = 1411)]
    public async Task GET_DistrictSparkline_Aggregates_Multiple_Deliveries_In_Same_Hour()
    {
        await ResetDatabaseAsync();
        await SeedDeliveryAsync("mezzeh");
        await SeedDeliveryAsync("mezzeh");
        await SeedDeliveryAsync("mezzeh");

        var client = AuthClient();
        var response = await client.GetAsync("/api/districts/mezzeh/sparkline?hours=6");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SparklinePoint>>();
        body.Should().NotBeNull();
        body!.Should().Contain(p => p.Count >= 3);
    }
}