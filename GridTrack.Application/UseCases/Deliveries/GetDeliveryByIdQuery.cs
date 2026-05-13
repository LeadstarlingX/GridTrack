using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record GetDeliveryRequest(Guid DeliveryId);

public sealed record GetDeliveryByIdQuery(GetDeliveryRequest Request);

public sealed class GetDeliveryByIdHandler
{
    public async Task<Result<DeliveryDto>> Handle(
        GetDeliveryByIdQuery query,
        IDeliveryReadService readService,
        CancellationToken ct)
    {
        var delivery = await readService.GetByIdAsync(query.Request.DeliveryId, ct);
        if (delivery is null)
        {
            return Result.Failure<DeliveryDto>(ApplicationErrors.DeliveryNotFound);
        }

        return Result.Success(delivery);
    }
}
