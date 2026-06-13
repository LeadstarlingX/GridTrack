using GridTrack.Application.CQRS.Handlers;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Application.IntegrationEvents;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

file sealed class NullDeliveryReadService : IDeliveryReadService
{
    public Task<Delivery?> GetAggregateByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Delivery?>(null);
    public Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    public Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct) => throw new NotImplementedException();
    public Task<IEnumerable<RouteWaypointDto>> GetRouteAsync(Guid deliveryId, CancellationToken ct) => throw new NotImplementedException();
    public Task<GetDeliveriesResponse> GetAllPaginatedAsync(string? cursor, string? status, string? districtId, DateTime? from, DateTime? to, int pageSize, CancellationToken ct) => throw new NotImplementedException();
}

file sealed class NoOpDeliveryRepository : IDeliveryRepository
{
    public Task AddAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateAsync(Delivery delivery, CancellationToken ct) => Task.CompletedTask;
}

file sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

public class UrgencyResultHandlerTests
{
    private static (IDeliveryReadService, IDeliveryRepository, IUnitOfWork) NullDeps()
        => (new NullDeliveryReadService(), new NoOpDeliveryRepository(), new NoOpUnitOfWork());

    [Test]
    public async Task Handle_Should_Cache_Message_Under_Urgency_Key()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var (reads, repo, uow) = NullDeps();
        var deliveryId = Guid.NewGuid();
        var msg = new UrgencyResultMessage(deliveryId, UrgencyScore: 8, AiNote: "Driver is significantly delayed.");

        await UrgencyResultHandler.Handle(msg, cache, push, reads, repo, uow, CancellationToken.None);

        await Assert.That(cache.SetCalls).Count().IsEqualTo(1);
        await Assert.That(cache.SetCalls[0].Key).IsEqualTo($"urgency:{deliveryId}");
        await Assert.That(cache.SetCalls[0].Value).IsEqualTo(msg);
    }

    [Test]
    public async Task Handle_Should_Cache_With_Ten_Minute_Expiration()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var (reads, repo, uow) = NullDeps();
        var msg = new UrgencyResultMessage(Guid.NewGuid(), 5, "Moderate delay.");

        await UrgencyResultHandler.Handle(msg, cache, push, reads, repo, uow, CancellationToken.None);

        await Assert.That(cache.SetCalls[0].Expiration).IsEqualTo(TimeSpan.FromMinutes(10));
    }

    [Test]
    public async Task Handle_Should_Broadcast_Urgency_To_Push_Service()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var (reads, repo, uow) = NullDeps();
        var deliveryId = Guid.NewGuid();
        var msg = new UrgencyResultMessage(deliveryId, UrgencyScore: 9, AiNote: "Critical eta breach.");

        await UrgencyResultHandler.Handle(msg, cache, push, reads, repo, uow, CancellationToken.None);

        await Assert.That(push.UrgencyCalls).Count().IsEqualTo(1);
        await Assert.That(push.UrgencyCalls[0].DeliveryId).IsEqualTo(deliveryId);
        await Assert.That(push.UrgencyCalls[0].Score).IsEqualTo(9);
        await Assert.That(push.UrgencyCalls[0].Note).IsEqualTo("Critical eta breach.");
    }

    [Test]
    public async Task Handle_Should_Cache_And_Broadcast_In_Same_Call()
    {
        var cache = new FakeCacheService();
        var push = new FakeDashboardPushService();
        var (reads, repo, uow) = NullDeps();
        var msg = new UrgencyResultMessage(Guid.NewGuid(), 3, "Minor delay.");

        await UrgencyResultHandler.Handle(msg, cache, push, reads, repo, uow, CancellationToken.None);

        await Assert.That(cache.SetCalls).Count().IsEqualTo(1);
        await Assert.That(push.UrgencyCalls).Count().IsEqualTo(1);
    }
}
