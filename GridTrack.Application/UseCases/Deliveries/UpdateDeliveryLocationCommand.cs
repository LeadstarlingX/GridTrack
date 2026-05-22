using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Errors;
using GridTrack.Domain.Abstractions;
using NetTopologySuite.Geometries;
using System.Linq;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record UpdateLocationRequest(Guid DeliveryId, Point Location, DateTime Timestamp);

public sealed record UpdateDeliveryLocationCommand(UpdateLocationRequest Request);

public sealed class UpdateDeliveryLocationHandler
{
    public async Task<(Result Result, IEnumerable<object> Events)> Handle(
        UpdateDeliveryLocationCommand command,
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

        var updateResult = delivery.UpdateLocation(request.Location, request.Timestamp);
        if (updateResult.IsFailure)
        {
            return (updateResult, Array.Empty<object>());
        }

        await repository.UpdateAsync(delivery, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var events = delivery.DomainEvents.Cast<object>().ToList();
        delivery.ClearDomainEvents();

        return (Result.Success(), events);
    }
}
