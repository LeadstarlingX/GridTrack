using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Analytics;

namespace GridTrack.Application.UnitTests.UseCases.Analytics;

public class GetHeatmapDataHandlerTests
{
    [Test]
    public async Task Handle_Returns_Success_With_Points_From_ReadService()
    {
        var expected = new[]
        {
            new HeatmapPointDto { Latitude = 33.51m, Longitude = 36.27m, Intensity = 1.0 },
            new HeatmapPointDto { Latitude = 33.52m, Longitude = 36.28m, Intensity = 0.8 },
        };
        var handler = new GetHeatmapDataHandler();

        var result = await handler.Handle(
            new GetHeatmapDataQuery(new HeatmapRequest("mezzeh", DateTime.UtcNow.AddHours(-1))),
            new FakeHeatmapReadService(expected),
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Handle_Returns_Success_With_Empty_When_No_Points()
    {
        var handler = new GetHeatmapDataHandler();

        var result = await handler.Handle(
            new GetHeatmapDataQuery(new HeatmapRequest("mezzeh", DateTime.UtcNow.AddHours(-1))),
            new FakeHeatmapReadService([]),
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEmpty();
    }

    [Test]
    public async Task Handle_Passes_DistrictId_And_Window_To_ReadService()
    {
        var fake = new FakeHeatmapReadService([]);
        var window = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var handler = new GetHeatmapDataHandler();

        await handler.Handle(
            new GetHeatmapDataQuery(new HeatmapRequest("kafrsousa", window)),
            fake,
            CancellationToken.None);

        await Assert.That(fake.LastDistrictId).IsEqualTo("kafrsousa");
        await Assert.That(fake.LastWindow).IsEqualTo(window);
    }

    // ── Fake ──────────────────────────────────────────────────────────────

    private sealed class FakeHeatmapReadService(IEnumerable<HeatmapPointDto> points) : IHeatmapReadService
    {
        public string? LastDistrictId { get; private set; }
        public DateTime LastWindow { get; private set; }

        public Task<IEnumerable<HeatmapPointDto>> GetHeatmapAsync(
            string districtId, DateTime window, CancellationToken ct)
        {
            LastDistrictId = districtId;
            LastWindow = window;
            return Task.FromResult(points);
        }
    }
}
