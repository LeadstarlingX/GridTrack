using GridTrack.Application.Abstractions.Clock;
using GridTrack.Application.Dtos;
using GridTrack.Application.EventHandlers;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using NetTopologySuite.Geometries;

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
    public async Task<Result<DeliveryCreatedResponse>> Handle(
        CreateDeliveryCommand command,
        IDeliveryRepository repository,
        IH3GridService h3GridService,
        IDateTimeProvider dateTimeProvider,
        IEventPublisher eventPublisher,
        CancellationToken ct)
    {
        var request = command.Request;
        var districtId = request.DistrictId;

        if (string.IsNullOrWhiteSpace(districtId))
        {
            districtId = await h3GridService.GetCellIndexForPointAsync(request.CurrentLocation, request.H3Resolution);
        }

        var deliveryResult = Delivery.Create(
            request.DeliveryId,
            request.CurrentLocation,
            districtId,
            dateTimeProvider.UtcNow,
            request.ExpectedEta);

        if (deliveryResult.IsFailure)
        {
            return Result.Failure<DeliveryCreatedResponse>(deliveryResult.Error);
        }

        await repository.AddAsync(deliveryResult.Value, ct);
        await DomainEventDispatcher.PublishAsync(deliveryResult.Value, eventPublisher, ct);

        return Result.Success(new DeliveryCreatedResponse(
            deliveryResult.Value.DeliveryId,
            deliveryResult.Value.DistrictId,
            deliveryResult.Value.CreatedAt));
    }
}
