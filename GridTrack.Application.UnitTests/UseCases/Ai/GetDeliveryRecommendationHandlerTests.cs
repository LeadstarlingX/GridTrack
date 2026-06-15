using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Ai;
using GridTrack.Application.UnitTests.CQRS.Handlers;
using GridTrack.Domain.Deliveries;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UnitTests.UseCases.Ai;

public class GetDeliveryRecommendationHandlerTests
{
    private static readonly GeometryFactory Geo = new();
    private static Point Location => Geo.CreatePoint(new Coordinate(36.3, 33.5));

    private static Delivery MakeDelivery(Guid? id = null)
    {
        var d = Delivery.Create(id ?? Guid.NewGuid(), Location, "mezzeh", DateTime.UtcNow, null).Value;
        d.ClearDomainEvents();
        return d;
    }

    private static DispatchCandidateDto MakeCandidate(Guid? driverId = null) =>
        new(driverId ?? Guid.NewGuid(), "Driver A", "DA", "mezzeh", 500, 0.9, 1, 1.0, 1.0);

    // ── 1. Cache hit ────────────────────────────────────────────────────────

    [Test]
    public async Task Handle_Returns_Cached_Result_Without_Calling_Downstream_Services()
    {
        var deliveryId = Guid.NewGuid();
        var cached = BuildResponse(deliveryId, [MakeCandidate()]);

        var cache = new FakeCacheService();
        await cache.SetAsync($"recommend:{deliveryId}", cached, TimeSpan.FromMinutes(5));
        var setCountAfterSeed = cache.SetCalls.Count; // = 1 (the seed itself)

        var ai      = new FakeAiRecommendationService(null);
        var read    = new FakeDeliveryReadService(null);
        var strategy = new FakeDispatchStrategy([]);

        var result = await new GetDeliveryRecommendationHandler().Handle(
            new GetDeliveryRecommendationQuery(deliveryId),
            read, strategy, ai, cache,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.DeliveryId).IsEqualTo(deliveryId);
        await Assert.That(ai.CallCount).IsEqualTo(0);
        await Assert.That(read.AggregateLookupCount).IsEqualTo(0);
        // Handler must not have called Set again on a cache hit
        await Assert.That(cache.SetCalls.Count).IsEqualTo(setCountAfterSeed);
    }

    // ── 2. Cache miss — full path ────────────────────────────────────────────

    [Test]
    public async Task Handle_Calls_Ai_And_Caches_Result_On_Cache_Miss()
    {
        var delivery  = MakeDelivery();
        var candidate = MakeCandidate();
        var cache     = new FakeCacheService();
        var ai        = new FakeAiRecommendationService(new AiRecommendationResponse("Reassign", 1, "Driver nearby", 6));
        var read      = new FakeDeliveryReadService(delivery);
        var strategy  = new FakeDispatchStrategy([candidate]);

        var result = await new GetDeliveryRecommendationHandler().Handle(
            new GetDeliveryRecommendationQuery(delivery.DeliveryId),
            read, strategy, ai, cache,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(ai.CallCount).IsEqualTo(1);

        await Assert.That(cache.SetCalls).Count().IsEqualTo(1);
        await Assert.That(cache.SetCalls[0].Key).IsEqualTo($"recommend:{delivery.DeliveryId}");
        await Assert.That(cache.SetCalls[0].Expiration).IsEqualTo(TimeSpan.FromMinutes(5));
    }

    // ── 3. Delivery not found ────────────────────────────────────────────────

    [Test]
    public async Task Handle_Returns_Failure_When_Delivery_Not_Found()
    {
        var cache = new FakeCacheService();
        var ai    = new FakeAiRecommendationService(null);
        var read  = new FakeDeliveryReadService(null);

        var result = await new GetDeliveryRecommendationHandler().Handle(
            new GetDeliveryRecommendationQuery(Guid.NewGuid()),
            read, new FakeDispatchStrategy([]), ai, cache,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(ApplicationErrors.DeliveryNotFound);
        await Assert.That(cache.SetCalls).Count().IsEqualTo(0);
        await Assert.That(ai.CallCount).IsEqualTo(0);
    }

    // ── 4. AI unavailable — degraded response is cached ─────────────────────

    [Test]
    public async Task Handle_Caches_Degraded_Response_When_Ai_Unavailable()
    {
        var delivery = MakeDelivery();
        var cache    = new FakeCacheService();
        var ai       = new FakeAiRecommendationService(null); // Python down
        var read     = new FakeDeliveryReadService(delivery);

        var result = await new GetDeliveryRecommendationHandler().Handle(
            new GetDeliveryRecommendationQuery(delivery.DeliveryId),
            read, new FakeDispatchStrategy([MakeCandidate()]), ai, cache,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.AiAvailable).IsFalse();
        await Assert.That(result.Value.RecommendedAction).IsNull();
        await Assert.That(result.Value.RecommendedDriverId).IsNull();
        // Degraded response is still cached to spare Python during outages
        await Assert.That(cache.SetCalls).Count().IsEqualTo(1);
    }

    // ── 5. AI rank maps to correct driver ────────────────────────────────────

    [Test]
    public async Task Handle_Maps_Ai_Rank_To_Correct_Candidate_Driver()
    {
        var delivery   = MakeDelivery();
        var driver1    = MakeCandidate();
        var driver2    = MakeCandidate();
        var cache      = new FakeCacheService();
        // Rank 2 → second candidate
        var ai         = new FakeAiRecommendationService(new AiRecommendationResponse("Reassign", 2, "Second is closer", 5));
        var strategy   = new FakeDispatchStrategy([driver1, driver2]);

        var result = await new GetDeliveryRecommendationHandler().Handle(
            new GetDeliveryRecommendationQuery(delivery.DeliveryId),
            new FakeDeliveryReadService(delivery), strategy, ai, cache,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.RecommendedDriverId).IsEqualTo(driver2.DriverId);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private static DeliveryRecommendationResponse BuildResponse(Guid deliveryId, IReadOnlyList<DispatchCandidateDto> candidates) =>
        new(deliveryId, "mezzeh", candidates, "Monitor", null, "All fine", 3, AiAvailable: true);

    private sealed class FakeDeliveryReadService(Delivery? delivery) : IDeliveryReadService
    {
        public int AggregateLookupCount { get; private set; }

        public Task<Delivery?> GetAggregateByIdAsync(Guid id, CancellationToken ct)
        {
            AggregateLookupCount++;
            return Task.FromResult(delivery);
        }

        public Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<DeliveryDto?>(null);

        public Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
            => Task.FromResult<IEnumerable<DeliveryDto>>([]);

        public Task<IEnumerable<RouteWaypointDto>> GetRouteAsync(Guid deliveryId, CancellationToken ct)
            => Task.FromResult<IEnumerable<RouteWaypointDto>>([]);

        public Task<GetDeliveriesResponse> GetAllPaginatedAsync(
            string? cursor, string? status, string? districtId,
            DateTime? from, DateTime? to, int pageSize, CancellationToken ct)
            => Task.FromResult(new GetDeliveriesResponse([], null, null));
    }

    private sealed class FakeDispatchStrategy(IReadOnlyList<DispatchCandidateDto> candidates) : IDispatchStrategy
    {
        public Task<IReadOnlyList<DispatchCandidateDto>> GetCandidatesAsync(Point deliveryLocation, int count, CancellationToken ct)
            => Task.FromResult(candidates);
    }

    private sealed class FakeAiRecommendationService(AiRecommendationResponse? response) : IAiRecommendationService
    {
        public int CallCount { get; private set; }

        public Task<AiRecommendationResponse?> GetAsync(AiRecommendationRequestDto request, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(response);
        }
    }
}
