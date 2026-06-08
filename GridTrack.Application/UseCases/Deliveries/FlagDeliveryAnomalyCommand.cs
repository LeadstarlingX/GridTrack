using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Errors;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.ValueObjects;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record FlagAnomalyRequest(Guid DeliveryId, AnomalyType Type, string Reason);

public sealed record FlagDeliveryAnomalyCommand(FlagAnomalyRequest Request);

public sealed class FlagDeliveryAnomalyHandler
{
    public async Task<(Result Result, DeliveryFlaggedAnomalousDomainEvent? Event)> Handle(
        FlagDeliveryAnomalyCommand command,
        IDeliveryReadService readService,
        IDeliveryRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var request = command.Request;
        var delivery = await readService.GetAggregateByIdAsync(request.DeliveryId, ct);

        if (delivery is null)
            return (Result.Failure(ApplicationErrors.DeliveryNotFound), null);

        var result = delivery.FlagAnomaly(request.Type, request.Reason);
        if (result.IsFailure)
            return (result, null);

        await repository.UpdateAsync(delivery, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var domainEvent = delivery.DomainEvents
            .OfType<DeliveryFlaggedAnomalousDomainEvent>()
            .FirstOrDefault();
        delivery.ClearDomainEvents();

        return (Result.Success(), domainEvent);
    }
}
