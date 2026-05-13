using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.Errors;

public static class ApplicationErrors
{
    public static readonly Error DeliveryNotFound = new("Application.DeliveryNotFound", "Delivery was not found.");
    public static readonly Error DriverNotFound = new("Application.DriverNotFound", "Driver was not found.");
    public static readonly Error ForecastNotFound = new("Application.ForecastNotFound", "Forecast data was not found.");
    public static readonly Error InvalidTelemetry = new("Application.InvalidTelemetry", "Telemetry batch is invalid.");
}
