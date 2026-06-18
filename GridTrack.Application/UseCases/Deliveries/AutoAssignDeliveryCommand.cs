using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dispatch;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using Microsoft.Extensions.Options;
using Wolverine;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record AutoAssignDeliveryCommand(Guid DeliveryId);

public sealed class AutoAssignDeliveryHandler
{
    public async Task<(Result<AutoAssignResponse> Result, DeliveryAssignedDomainEvent? Event)> Handle(
        AutoAssignDeliveryCommand command,
        IDeliveryReadService deliveryReadService,
        IDispatchStrategy strategy,
        IDeliveryRepository repository,
        IUnitOfWork unitOfWork,
        IOptions<DispatchWeightsOptions> weightsOpts,
        IMessageBus bus,
        CancellationToken ct)
    {
        var delivery = await deliveryReadService.GetAggregateByIdAsync(command.DeliveryId, ct);
        if (delivery is null)
            return (Result.Failure<AutoAssignResponse>(ApplicationErrors.DeliveryNotFound), null);

        var candidates = await strategy.GetCandidatesAsync(delivery.CurrentLocation, count: 5, ct);
        var topThree   = candidates.Take(3).ToList();

        if (candidates.Count == 0)
            return (Result.Success(new AutoAssignResponse(false, null, topThree)), null);

        var gap = candidates.Count == 1
            ? double.MaxValue
            : candidates[0].Score - candidates[1].Score;

        if (gap < weightsOpts.Value.AutoAssignGapThreshold)
            return (Result.Success(new AutoAssignResponse(false, null, topThree)), null);

        var assignResult = delivery.AssignDriver(candidates[0].DriverId);
        if (assignResult.IsFailure)
            return (Result.Failure<AutoAssignResponse>(assignResult.Error), null);

        await repository.UpdateAsync(delivery, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var domainEvent = delivery.DomainEvents.OfType<DeliveryAssignedDomainEvent>().SingleOrDefault();
        delivery.ClearDomainEvents();

        // Publish route calculation off the HTTP thread — OSRM is called async via Wolverine
        // local queue so the HTTP response returns before route geometry is fetched.
        await bus.PublishAsync(new RouteCalculationMessage(command.DeliveryId, candidates[0].DriverId));

        return (Result.Success(new AutoAssignResponse(true, candidates[0].DriverId, topThree)), domainEvent);
    }
}
