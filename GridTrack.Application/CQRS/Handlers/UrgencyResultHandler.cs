using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.IntegrationEvents;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.CQRS.Handlers;

public static class UrgencyResultHandler
{
    public static async Task Handle(
        UrgencyResultMessage msg,
        ICacheService cache,
        IDashboardPushService push,
        IDeliveryReadService readService,
        IDeliveryRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        await cache.SetAsync(
            $"urgency:{msg.DeliveryId}", msg, TimeSpan.FromMinutes(10), ct);

        var delivery = await readService.GetAggregateByIdAsync(msg.DeliveryId, ct);
        if (delivery is not null)
        {
            var setResult = delivery.SetUrgencyScore(msg.UrgencyScore, DateTime.UtcNow);
            if (setResult.IsSuccess)
            {
                await repository.UpdateAsync(delivery, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
        }

        await push.BroadcastUrgencyUpdateAsync(
            msg.DeliveryId, delivery?.DistrictId, msg.UrgencyScore, msg.AiNote, ct);
    }
}
