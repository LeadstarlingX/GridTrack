namespace GridTrack.Application.UseCases.Drivers;

public sealed record RecalculateDeliveryEtaMessage(
    Guid DriverId,
    double DriverLat,
    double DriverLng);
