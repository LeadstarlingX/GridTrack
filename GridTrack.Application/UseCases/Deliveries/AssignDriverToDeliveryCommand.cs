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
    public async Task<(Result Result, DeliveryAssignedDomainEvent[] Events)> Handle(
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
            return (Result.Failure(ApplicationErrors.DeliveryNotFound), []);
        }

        var result = delivery.AssignDriver(request.DriverId);
        if (result.IsFailure)
        {
            return (result, []);
        }

        await repository.UpdateAsync(delivery, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var events = delivery.DomainEvents.OfType<DeliveryAssignedDomainEvent>().ToArray();
        delivery.ClearDomainEvents();

        
        return (Result.Success(), events);
    }
}