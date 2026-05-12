namespace GridTrack.Domain.ValueObjects;

public enum DeliveryStatus
{
	Created,
	Assigned,
	PickedUp,
	InTransit,
	Delivered,
	Cancelled,
	Anomalous
}