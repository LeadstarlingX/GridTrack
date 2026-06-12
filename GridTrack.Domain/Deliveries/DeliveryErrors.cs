using GridTrack.Domain.Abstractions;

namespace GridTrack.Domain.Deliveries;

public static class DeliveryErrors
{
    public static readonly Error InvalidDeliveryId = new("Delivery.InvalidId", "DeliveryId cannot be empty.");
    public static readonly Error InvalidLocation = new("Delivery.InvalidLocation", "Location cannot be null.");
    public static readonly Error InvalidDistrictId = new("Delivery.InvalidDistrictId", "DistrictId cannot be empty.");
    public static readonly Error InvalidDriverId = new("Delivery.InvalidDriverId", "DriverId cannot be empty.");
    public static readonly Error InvalidReason = new("Delivery.InvalidReason", "Reason cannot be empty.");
    public static readonly Error TerminalStatus = new("Delivery.TerminalStatus", "Delivery is already in a terminal status.");
    public static readonly Error InvalidStatusTransition = new("Delivery.InvalidStatusTransition", "Invalid delivery status transition.");
    public static readonly Error InvalidStatusForOperation = new("Delivery.InvalidStatusForOperation", "Delivery status does not allow this operation.");
    public static readonly Error AlreadyFlagged = new("Delivery.AlreadyFlagged", "Delivery has already been flagged as anomalous.");
}
