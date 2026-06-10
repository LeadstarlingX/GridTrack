using System.Diagnostics;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Deliveries;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using GridTrack.IntegrationTests.Abstractions;
using NSubstitute;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.ApplicationTests;

/// <summary>
/// Verifies that domain events raised inside command handlers actually dispatch through
/// Wolverine's cascade to the broadcast handlers (A1). These tests are the regression
/// guard against the "IEnumerable&lt;object&gt; cascade is silently dropped" hypothesis:
/// if the cascade were dropped, the push service spy would never be called.
/// </summary>
public class CascadeDispatchTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    // Cascaded messages may be dispatched on a Wolverine background worker after
    // InvokeAsync returns, so poll the spy for a short window before failing.
    private static async Task AssertEventuallyAsync(Action assertion, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            try { assertion(); return; }
            catch when (sw.ElapsedMilliseconds < timeoutMs) { await Task.Delay(50); }
        }
    }

    [Test]
    [NotInParallel(Order = 200)]
    public async Task UpdateDriverPosition_Should_Dispatch_DriverPositionBroadcast()
    {
        await ResetDatabaseAsync();
        Factory.DashboardPushMock.ClearReceivedCalls();

        var driverId = Guid.NewGuid();
        await InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                driverId, Damascus, 9, "mezzeh", "Ahmad Hassan", "Ahmad", true)));

        var result = await InvokeAsync<Result>(
            new UpdateDriverPositionCommand(new UpdatePositionRequest(driverId, Damascus, DateTime.UtcNow)));

        await Assert.That(result.IsSuccess).IsTrue();
        await AssertEventuallyAsync(() => Factory.DashboardPushMock
            .Received()
            .BroadcastDriverPositionAsync(
                Arg.Any<string>(), Arg.Is<DriverDto>(d => d.DriverId == driverId), Arg.Any<CancellationToken>()));
    }

    [Test]
    [NotInParallel(Order = 201)]
    public async Task CancelDelivery_After_Eta_Should_Dispatch_AnomalyBroadcast()
    {
        await ResetDatabaseAsync();
        Factory.DashboardPushMock.ClearReceivedCalls();

        var createdAt = DateTime.UtcNow.AddHours(-1);
        var expectedEta = createdAt.AddMinutes(20);
        var delivery = Delivery.Create(Guid.NewGuid(), Damascus, "mezzeh", createdAt, expectedEta).Value;
        delivery.ClearDomainEvents();
        await SeedDeliveriesAsync([delivery]);

        var result = await InvokeAsync<Result>(
            new CancelDeliveryCommand(new CancelDeliveryRequest(
                delivery.DeliveryId, expectedEta.AddMinutes(10), "client unreachable")));

        await Assert.That(result.IsSuccess).IsTrue();
        await AssertEventuallyAsync(() => Factory.DashboardPushMock
            .Received()
            .BroadcastAnomalyAsync(
                Arg.Any<string>(),
                Arg.Is<AnomalyAlertDto>(a => a.DeliveryId == delivery.DeliveryId),
                Arg.Any<CancellationToken>()));
    }
}
