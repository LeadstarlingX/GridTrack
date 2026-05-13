using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Common;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record GetDeliveriesByDistrictQuery(DistrictFilterRequest Request);

public sealed class GetDeliveriesByDistrictHandler
{
    public async Task<Result<IEnumerable<DeliveryDto>>> Handle(
        GetDeliveriesByDistrictQuery query,
        IDeliveryReadService readService,
        CancellationToken ct)
    {
        var deliveries = await readService.GetByDistrictAsync(query.Request.DistrictId, ct);
        return Result.Success(deliveries);
    }
}
