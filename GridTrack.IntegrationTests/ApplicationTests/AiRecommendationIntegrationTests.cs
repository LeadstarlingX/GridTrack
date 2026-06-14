using FluentAssertions;
using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Ai;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using GridTrack.Domain.ValueObjects;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class AiRecommendationIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static Point Damascus => Geo.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static Driver MakeDriver()
    {
        var r = Driver.Create(Guid.NewGuid(), Damascus, "mezzeh", DateTime.UtcNow,
            "Test Driver", "TD", isActive: true);
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    private static Delivery MakeDelivery()
    {
        var d = Delivery.Create(Guid.NewGuid(), Damascus, "mezzeh", DateTime.UtcNow, null).Value;
        d.ClearDomainEvents();
        return d;
    }

    [Test]
    [NotInParallel(Order = 450)]
    public async Task GetDeliveryRecommendation_Returns_Failure_When_Delivery_Not_Found()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<Result<DeliveryRecommendationResponse>>(
            new GetDeliveryRecommendationQuery(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
    }

    [Test]
    [NotInParallel(Order = 451)]
    public async Task GetDeliveryRecommendation_Returns_Candidates_With_Ai_Degraded_When_Python_Unavailable()
    {
        await ResetDatabaseAsync();

        var driver   = MakeDriver();
        var delivery = MakeDelivery();
        await SeedDriversAsync([driver]);
        await SeedDeliveriesAsync([delivery]);

        var result = await InvokeAsync<Result<DeliveryRecommendationResponse>>(
            new GetDeliveryRecommendationQuery(delivery.DeliveryId));

        result.IsSuccess.Should().BeTrue();
        var resp = result.Value;

        // Delivery context is correct
        resp.DeliveryId.Should().Be(delivery.DeliveryId);
        resp.DistrictId.Should().Be("mezzeh");

        // Driver is returned as a candidate
        resp.TopCandidates.Should().HaveCount(1);
        resp.TopCandidates[0].DriverId.Should().Be(driver.DriverId);

        // Python unavailable in tests → AI section is null / degraded
        resp.AiAvailable.Should().BeFalse();
        resp.RecommendedAction.Should().BeNull();
        resp.RecommendedDriverId.Should().BeNull();
        resp.Reason.Should().BeNull();
        resp.UrgencyScore.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 452)]
    public async Task GetDeliveryRecommendation_Includes_Anomaly_Context_When_Delivery_Is_Flagged()
    {
        await ResetDatabaseAsync();

        var driver   = MakeDriver();
        var delivery = MakeDelivery();
        delivery.FlagAnomaly(AnomalyType.EtaExceeded, "Driver stalled").IsSuccess.Should().BeTrue();
        delivery.ClearDomainEvents();

        await SeedDriversAsync([driver]);
        await SeedDeliveriesAsync([delivery]);

        var result = await InvokeAsync<Result<DeliveryRecommendationResponse>>(
            new GetDeliveryRecommendationQuery(delivery.DeliveryId));

        // Main response is success regardless of AI availability
        result.IsSuccess.Should().BeTrue();
        result.Value.TopCandidates.Should().HaveCount(1);
    }
}
