using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.UnitTests.CQRS.Handlers;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Ai;

namespace GridTrack.Application.UnitTests.UseCases.Ai;

public class GetDistrictSummaryHandlerTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DistrictContextDto MakeCtx(string districtId = "mezzeh") =>
        new(districtId, ActiveDeliveries: 10, ActiveDrivers: 3, AnomalyRate24h: 0.05);

    // ── Cache hit ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Handle_Returns_Cached_Summary_Without_Calling_AI()
    {
        var cache = new FakeCacheService();
        var cached = new DistrictSummaryResponse("mezzeh", "Cached summary", _now, CachedAt: null);
        await cache.SetAsync("ai:district-summary:mezzeh", cached, TimeSpan.FromMinutes(2), CancellationToken.None);

        var chatService = new FakeAnalysisChatService(null);
        var handler = new GetDistrictSummaryHandler();
        var result = await handler.Handle(
            new GetDistrictSummaryQuery("mezzeh"),
            new FakeDistrictReadService(MakeCtx()),
            chatService,
            cache,
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Summary).IsEqualTo("Cached summary");
        await Assert.That(chatService.AskCallCount).IsEqualTo(0);
    }

    // ── Cache miss → AI returns summary ──────────────────────────────────────

    [Test]
    public async Task Handle_Returns_AI_Summary_On_Cache_Miss()
    {
        var handler = new GetDistrictSummaryHandler();
        var result = await handler.Handle(
            new GetDistrictSummaryQuery("mezzeh"),
            new FakeDistrictReadService(MakeCtx()),
            new FakeAnalysisChatService("Prioritise kafrsousa."),
            new FakeCacheService(),
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Summary).IsEqualTo("Prioritise kafrsousa.");
    }

    [Test]
    public async Task Handle_Sets_Both_Fresh_And_Stale_Cache_Entries_When_AI_Returns_Summary()
    {
        var cache = new FakeCacheService();
        var handler = new GetDistrictSummaryHandler();
        await handler.Handle(
            new GetDistrictSummaryQuery("mezzeh"),
            new FakeDistrictReadService(MakeCtx()),
            new FakeAnalysisChatService("summary"),
            cache,
            CancellationToken.None);

        // Two SetAsync calls: fresh key + stale key
        await Assert.That(cache.SetCalls).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Handle_Passes_DistrictId_In_Question_To_AI()
    {
        var chatService = new FakeAnalysisChatService("ok");
        var handler = new GetDistrictSummaryHandler();
        await handler.Handle(
            new GetDistrictSummaryQuery("kafrsousa"),
            new FakeDistrictReadService(MakeCtx("kafrsousa")),
            chatService,
            new FakeCacheService(),
            CancellationToken.None);

        await Assert.That(chatService.LastQuestion).Contains("kafrsousa");
    }

    [Test]
    public async Task Handle_Passes_Context_Metrics_To_AI()
    {
        var chatService = new FakeAnalysisChatService("ok");
        var handler = new GetDistrictSummaryHandler();
        await handler.Handle(
            new GetDistrictSummaryQuery("mezzeh"),
            new FakeDistrictReadService(new DistrictContextDto("mezzeh", 42, 7, 0.12)),
            chatService,
            new FakeCacheService(),
            CancellationToken.None);

        await Assert.That(chatService.LastCsvContext).Contains("active_deliveries=42");
        await Assert.That(chatService.LastCsvContext).Contains("active_drivers=7");
    }

    // ── Cache miss → AI unavailable ───────────────────────────────────────────

    [Test]
    public async Task Handle_Returns_Null_When_AI_Unavailable_And_No_Stale_Cache()
    {
        var handler = new GetDistrictSummaryHandler();
        var result = await handler.Handle(
            new GetDistrictSummaryQuery("mezzeh"),
            new FakeDistrictReadService(MakeCtx()),
            new FakeAnalysisChatService(null),
            new FakeCacheService(),
            CancellationToken.None);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Handle_Returns_Stale_Cache_When_AI_Unavailable()
    {
        var cache = new FakeCacheService();
        var stale = new DistrictSummaryResponse("mezzeh", "Old summary", _now.AddHours(-2), CachedAt: _now.AddHours(-2));
        await cache.SetAsync("ai:district-summary:mezzeh:stale", stale, TimeSpan.FromHours(1), CancellationToken.None);

        var handler = new GetDistrictSummaryHandler();
        var result = await handler.Handle(
            new GetDistrictSummaryQuery("mezzeh"),
            new FakeDistrictReadService(MakeCtx()),
            new FakeAnalysisChatService(null),
            cache,
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Summary).IsEqualTo("Old summary");
    }

    [Test]
    public async Task Handle_Does_Not_Write_Cache_When_AI_Returns_Null()
    {
        var cache = new FakeCacheService();
        var handler = new GetDistrictSummaryHandler();
        await handler.Handle(
            new GetDistrictSummaryQuery("mezzeh"),
            new FakeDistrictReadService(MakeCtx()),
            new FakeAnalysisChatService(null),
            cache,
            CancellationToken.None);

        await Assert.That(cache.SetCalls).Count().IsEqualTo(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class FakeDistrictReadService(DistrictContextDto ctx) : IDistrictReadService
    {
        public Task<DistrictContextDto> GetDistrictContextAsync(string districtId, CancellationToken ct) =>
            Task.FromResult(ctx);

        public Task<GetDistrictsResponse> GetDistrictsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<GetDistrictBoundariesResponse> GetDistrictBoundariesAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeAnalysisChatService(string? reply) : IAnalysisChatService
    {
        public int AskCallCount { get; private set; }
        public string LastQuestion { get; private set; } = string.Empty;
        public string LastCsvContext { get; private set; } = string.Empty;

        public Task<string?> AskAsync(string question, string csvContext, CancellationToken ct)
        {
            AskCallCount++;
            LastQuestion = question;
            LastCsvContext = csvContext;
            return Task.FromResult(reply);
        }

        public IAsyncEnumerable<string> StreamAsync(string question, string csvContext, CancellationToken ct) => throw new NotImplementedException();
        public Task<string?> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct) => throw new NotImplementedException();
        public Task<byte[]?> GenerateReportAsync(IEnumerable<ChatMessageDto> messages, string csvContext, CancellationToken ct) => throw new NotImplementedException();
    }
}
