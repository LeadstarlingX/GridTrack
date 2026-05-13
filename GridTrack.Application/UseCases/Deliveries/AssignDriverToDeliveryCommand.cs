using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using System.Linq;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record AssignDriverRequest(Guid DeliveryId, Guid DriverId);

public sealed record AssignDriverToDeliveryCommand(AssignDriverRequest Request);

public sealed class AssignDriverToDeliveryHandler
{
    public async Task<(Result Result, IEnumerable<object> Events)> Handle(
        AssignDriverToDeliveryCommand command,
        IDeliveryRepository repository,
        CancellationToken ct)
    {
        var request = command.Request;
        var delivery = await repository.GetByIdAsync(request.DeliveryId, ct);

        if (delivery is null)
        {
            return (Result.Failure(ApplicationErrors.DeliveryNotFound), Array.Empty<object>());
        }

        var result = delivery.AssignDriver(request.DriverId);
        if (result.IsFailure)
        {
            return (result, Array.Empty<object>());
        }

        await repository.UpdateAsync(delivery, ct);
        var events = delivery.DomainEvents.Cast<object>().ToList();
        delivery.ClearDomainEvents();

        return (Result.Success(), events);
    }
}
