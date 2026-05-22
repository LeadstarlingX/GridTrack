using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Errors;
using GridTrack.Domain.Abstractions;
using NetTopologySuite.Geometries;
using System.Linq;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record PickUpDeliveryRequest(Guid DeliveryId, Point Location, DateTime Timestamp);

public sealed record MarkDeliveryPickedUpCommand(PickUpDeliveryRequest Request);

public sealed class MarkDeliveryPickedUpHandler
{
    public async Task<(Result Result, IEnumerable<object> Events)> Handle(
        MarkDeliveryPickedUpCommand command,
        IDeliveryReadService readService,
        IDeliveryRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var request = command.Request;
        var delivery = await readService.GetAggregateByIdAsync(request.DeliveryId, ct);

        if (delivery is null)
        {
            return (Result.Failure(ApplicationErrors.DeliveryNotFound), Array.Empty<object>());
        }

        var result = delivery.MarkPickedUp(request.Location, request.Timestamp);
        if (result.IsFailure)
        {
            return (result, Array.Empty<object>());
        }

        await repository.UpdateAsync(delivery, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var events = delivery.DomainEvents.Cast<object>().ToList();
        delivery.ClearDomainEvents();

        return (Result.Success(), events);
    }
}
