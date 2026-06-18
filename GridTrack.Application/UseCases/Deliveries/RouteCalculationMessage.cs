namespace GridTrack.Application.UseCases.Deliveries;

/// <summary>
/// Published after a driver is assigned to a delivery.
/// Handled off the HTTP request thread — route calculation never blocks the API response.
/// </summary>
public sealed record RouteCalculationMessage(Guid DeliveryId, Guid DriverId);
