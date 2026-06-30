using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Domain.Abstractions;
using System.Linq;
using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record AssignDriverRequest(Guid DeliveryId, Guid DriverId);

public sealed record AssignDriverToDeliveryCommand(AssignDriverRequest Request);

public sealed class AssignDriverToDeliveryHandler
{
    public async Task<(Result Result, DeliveryAssignedDomainEvent[] Events, RouteCalculationMessage? RouteCalculation)> Handle(
        AssignDriverToDeliveryCommand command,
        IDeliveryReadService readService,
        IDeliveryRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var request = command.Request;
        var delivery = await readService.GetAggregateByIdAsync(request.DeliveryId, ct);

        if (delivery is null)
        {
            return (Result.Failure(ApplicationErrors.DeliveryNotFound), [], null);
        }

        var result = delivery.AssignDriver(request.DriverId);
        if (result.IsFailure)
        {
            return (result, [], null);
        }

        await repository.UpdateAsync(delivery, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var events = delivery.DomainEvents.OfType<DeliveryAssignedDomainEvent>().ToArray();
        delivery.ClearDomainEvents();

        // Manual assignment bypasses the dispatch strategy entirely, so unlike auto-assign
        // nothing else triggers route geometry/cost calculation. Cascade it as a third tuple
        // value — Wolverine treats each tuple item independently (equivalent to
        // bus.PublishAsync, routed through the same route-calculation local queue configured
        // in Program.cs) — so manually assigned deliveries get RouteCost/RoutePolyline too.
        var routeCalculation = new RouteCalculationMessage(request.DeliveryId, request.DriverId);

        return (Result.Success(), events, routeCalculation);
    }
}