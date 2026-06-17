using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using NetTopologySuite.Geometries;
using System.Linq;

namespace GridTrack.Application.UseCases.Deliveries;

public sealed record CreateDeliveryRequest(
    Guid DeliveryId,
    Point CurrentLocation,
    int H3Resolution,
    DateTime? ExpectedEta,
    string? DistrictId);

public sealed record CreateDeliveryCommand(CreateDeliveryRequest Request);

public sealed class CreateDeliveryHandler
{
    public async Task<(Result<DeliveryCreatedResponse> Result, IEnumerable<object> Events)> Handle(
        CreateDeliveryCommand command,
        IDeliveryRepository repository,
        IH3GridService h3GridService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var request = command.Request;
        var districtId = request.DistrictId;

        if (string.IsNullOrWhiteSpace(districtId))
        {
            var cellResult = await h3GridService.GetCellAsync(request.CurrentLocation, request.H3Resolution);
            if (cellResult.IsFailure)
                return (Result.Failure<DeliveryCreatedResponse>(cellResult.Error), Array.Empty<object>());
            districtId = cellResult.Value;
        }

        var deliveryResult = Delivery.Create(
            request.DeliveryId,
            request.CurrentLocation,
            districtId,
            dateTimeProvider.UtcNow,
            request.ExpectedEta);

        if (deliveryResult.IsFailure)
        {
            return (Result.Failure<DeliveryCreatedResponse>(deliveryResult.Error), Array.Empty<object>());
        }

        await repository.AddAsync(deliveryResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var events = deliveryResult.Value.DomainEvents.Cast<object>().ToList();
        deliveryResult.Value.ClearDomainEvents();

        return (Result.Success(new DeliveryCreatedResponse(
            deliveryResult.Value.DeliveryId,
            deliveryResult.Value.DistrictId,
            deliveryResult.Value.CreatedAt)), events);
    }
}
