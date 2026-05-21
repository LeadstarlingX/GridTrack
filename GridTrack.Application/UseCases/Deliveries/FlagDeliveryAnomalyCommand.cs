using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.ValueObjects;
using System.Linq;
using GridTrack.Application.CQRS.Repositories;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record FlagAnomalyRequest(Guid DeliveryId, AnomalyType Type, string Reason);

public sealed record FlagDeliveryAnomalyCommand(FlagAnomalyRequest Request);

public sealed class FlagDeliveryAnomalyHandler
{
    public async Task<(Result Result, IEnumerable<object> Events)> Handle(
        FlagDeliveryAnomalyCommand command,
        IDeliveryRepository repository,
        CancellationToken ct)
    {
        var request = command.Request;
        var delivery = await repository.GetByIdAsync(request.DeliveryId, ct);

        if (delivery is null)
        {
            return (Result.Failure(ApplicationErrors.DeliveryNotFound), Array.Empty<object>());
        }

        var result = delivery.FlagAnomaly(request.Type, request.Reason);
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
