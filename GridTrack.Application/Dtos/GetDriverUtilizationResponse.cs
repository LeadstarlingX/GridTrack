namespace GridTrack.Application.Dtos;

public sealed record DriverThroughputItemResponse(
    Guid DriverId,
    string Name,
    int CompletedToday,
    int ActiveDeliveries);

public sealed record GetDriverUtilizationResponse(
    int ActiveDrivers,
    int InactiveDrivers,
    double AvgActiveDeliveriesPerActiveDriver,
    IReadOnlyList<DriverThroughputItemResponse> TopDrivers);
