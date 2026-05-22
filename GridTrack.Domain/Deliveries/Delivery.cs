using GridTrack.Domain.Abstractions;
using GridTrack.Domain.ValueObjects;
using NetTopologySuite.Geometries;

namespace GridTrack.Domain.Deliveries;

public sealed class Delivery : BaseEntity
{
	private Delivery()
	{
	}

	private Delivery(
		Guid deliveryId,
		Point currentLocation,
		string districtId,
		DateTime createdAt,
		DateTime? expectedEta)
	{
		DeliveryId = deliveryId;
		CurrentLocation = currentLocation;
		DistrictId = districtId;
		CreatedAt = createdAt;
		ExpectedEta = expectedEta;
		Status = DeliveryStatus.Created;
	}

	public Guid DeliveryId { get; private set; }
	public Point CurrentLocation { get; private set; } = null!;
	public DeliveryStatus Status { get; private set; }
	public Guid? AssignedDriverId { get; private set; }
	public DateTime? ExpectedEta { get; private set; }
	public DateTime? ActualEta { get; private set; }
	public string DistrictId { get; private set; } = string.Empty;
	public bool AnomalyFlag { get; private set; }
	public DateTime CreatedAt { get; private set; }
	public DateTime? PickedUpAt { get; private set; }
	public DateTime? DeliveredAt { get; private set; }
	public DateTime? CancelledAt { get; private set; }
	public string? AnomalyReason { get; private set; }

	public static Result<Delivery> Create(
		Guid deliveryId,
		Point currentLocation,
		string districtId,
		DateTime createdAt,
		DateTime? expectedEta = null)
	{
		if (deliveryId == Guid.Empty)
		{
			return Result.Failure<Delivery>(DeliveryErrors.InvalidDeliveryId);
		}

		if (currentLocation is null)
		{
			return Result.Failure<Delivery>(DeliveryErrors.InvalidLocation);
		}

		if (string.IsNullOrWhiteSpace(districtId))
		{
			return Result.Failure<Delivery>(DeliveryErrors.InvalidDistrictId);
		}

		var delivery = new Delivery(deliveryId, currentLocation, districtId, createdAt, expectedEta);
		delivery.RaiseDomainEvent(new DeliveryCreatedDomainEvent(deliveryId, createdAt, districtId));
		return Result.Success(delivery);
	}

	public Result AssignDriver(Guid driverId)
	{
		var terminalCheck = EnsureNotTerminal();
		if (terminalCheck.IsFailure)
		{
			return terminalCheck;
		}

		if (driverId == Guid.Empty)
		{
			return Result.Failure(DeliveryErrors.InvalidDriverId);
		}

		if (Status != DeliveryStatus.Created && Status != DeliveryStatus.Assigned)
		{
			return Result.Failure(DeliveryErrors.InvalidStatusForOperation);
		}

		var transition = TransitionTo(DeliveryStatus.Assigned);
		if (transition.IsFailure)
		{
			return transition;
		}

		AssignedDriverId = driverId;
		RaiseDomainEvent(new DeliveryAssignedDomainEvent(DeliveryId, driverId));
		return Result.Success();
	}

	public Result MarkPickedUp(Point location, DateTime timestamp)
	{
		var terminalCheck = EnsureNotTerminal();
		if (terminalCheck.IsFailure)
		{
			return terminalCheck;
		}

		if (Status != DeliveryStatus.Assigned)
		{
			return Result.Failure(DeliveryErrors.InvalidStatusForOperation);
		}

		if (location is null)
		{
			return Result.Failure(DeliveryErrors.InvalidLocation);
		}

		var transition = TransitionTo(DeliveryStatus.PickedUp);
		if (transition.IsFailure)
		{
			return transition;
		}

		CurrentLocation = location;
		PickedUpAt = timestamp;
		RaiseDomainEvent(new DeliveryPickedUpDomainEvent(DeliveryId, location, timestamp));
		return Result.Success();
	}

	public Result UpdateLocation(Point location, DateTime timestamp)
	{
		var terminalCheck = EnsureNotTerminal();
		if (terminalCheck.IsFailure)
		{
			return terminalCheck;
		}

		if (Status != DeliveryStatus.PickedUp && Status != DeliveryStatus.InTransit)
		{
			return Result.Failure(DeliveryErrors.InvalidStatusForOperation);
		}

		if (location is null)
		{
			return Result.Failure(DeliveryErrors.InvalidLocation);
		}

		if (Status == DeliveryStatus.PickedUp)
		{
			var transition = TransitionTo(DeliveryStatus.InTransit);
			if (transition.IsFailure)
			{
				return transition;
			}
		}

		CurrentLocation = location;
		RaiseDomainEvent(new DeliveryLocationUpdatedDomainEvent(DeliveryId, location, timestamp));
		return Result.Success();
	}

	public Result MarkDelivered(DateTime timestamp)
	{
		var terminalCheck = EnsureNotTerminal();
		if (terminalCheck.IsFailure)
		{
			return terminalCheck;
		}

		if (Status != DeliveryStatus.InTransit && Status != DeliveryStatus.PickedUp)
		{
			return Result.Failure(DeliveryErrors.InvalidStatusForOperation);
		}

		var transition = TransitionTo(DeliveryStatus.Delivered);
		if (transition.IsFailure)
		{
			return transition;
		}

		DeliveredAt = timestamp;
		ActualEta = timestamp;
		var expectedSecs = ExpectedEta.HasValue
			? (ExpectedEta.Value - CreatedAt).TotalSeconds
			: 0;
		RaiseDomainEvent(new DeliveryCompletedDomainEvent(
			DeliveryId, timestamp, AssignedDriverId, PickedUpAt, expectedSecs));
		return Result.Success();
	}

	public Result FlagAnomaly(AnomalyType type, string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			return Result.Failure(DeliveryErrors.InvalidReason);
		}

		var transition = TransitionTo(DeliveryStatus.Anomalous);
		if (transition.IsFailure)
		{
			return transition;
		}

		AnomalyFlag = true;
		AnomalyReason = reason;
		RaiseDomainEvent(new DeliveryFlaggedAnomalousDomainEvent(DeliveryId, type, reason, DistrictId));
		return Result.Success();
	}

	public Result MarkCancelled(DateTime timestamp, string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			return Result.Failure(DeliveryErrors.InvalidReason);
		}

		var transition = TransitionTo(DeliveryStatus.Cancelled);
		if (transition.IsFailure)
		{
			return transition;
		}

		CancelledAt = timestamp;
		AnomalyReason = reason;
		RaiseDomainEvent(new DeliveryCancelledDomainEvent(DeliveryId, timestamp, reason));
		return Result.Success();
	}

	public Result TransitionTo(DeliveryStatus nextStatus)
	{
		if (!IsValidTransition(Status, nextStatus))
		{
			return Result.Failure(DeliveryErrors.InvalidStatusTransition);
		}

		Status = nextStatus;
		return Result.Success();
	}

	private Result EnsureNotTerminal()
	{
		if (Status == DeliveryStatus.Cancelled || Status == DeliveryStatus.Delivered)
		{
			return Result.Failure(DeliveryErrors.TerminalStatus);
		}

		return Result.Success();
	}

	private static bool IsValidTransition(DeliveryStatus current, DeliveryStatus next)
	{
		if (current == next)
		{
			return true;
		}

		return current switch
		{
			DeliveryStatus.Created => next is DeliveryStatus.Assigned or DeliveryStatus.Anomalous or DeliveryStatus.Cancelled,
			DeliveryStatus.Assigned => next is DeliveryStatus.PickedUp or DeliveryStatus.Anomalous or DeliveryStatus.Cancelled,
			DeliveryStatus.PickedUp => next is DeliveryStatus.InTransit or DeliveryStatus.Delivered or DeliveryStatus.Anomalous or DeliveryStatus.Cancelled,
			DeliveryStatus.InTransit => next is DeliveryStatus.Delivered or DeliveryStatus.Anomalous or DeliveryStatus.Cancelled,
			DeliveryStatus.Anomalous => next is DeliveryStatus.InTransit or DeliveryStatus.Delivered or DeliveryStatus.Cancelled,
			DeliveryStatus.Delivered => false,
			DeliveryStatus.Cancelled => false,
			_ => false
		};
	}
}