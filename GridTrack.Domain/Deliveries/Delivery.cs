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
	public AnomalyType? AnomalyTypeValue { get; private set; }
	public DateTime CreatedAt { get; private set; }
	public DateTime? PickedUpAt { get; private set; }
	public DateTime? DeliveredAt { get; private set; }
	public DateTime? CancelledAt { get; private set; }
	public string? AnomalyReason { get; private set; }
	public int? UrgencyScore { get; private set; }
	public DateTime? UrgencyScoreAt { get; private set; }

	// Route economics — populated once OSRM returns a route for the assigned driver.
	// Cost is in SYP, computed from distance + duration by the route-cost calculator.
	public double? RouteDistanceMeters { get; private set; }
	public double? RouteDurationSeconds { get; private set; }
	public decimal? RouteCost { get; private set; }

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
			DeliveryId, DistrictId, timestamp, AssignedDriverId, PickedUpAt, expectedSecs));
		return Result.Success();
	}

	public Result FlagAnomaly(AnomalyType type, string reason)
	{
		if (Status == DeliveryStatus.Anomalous)
		{
			return Result.Failure(DeliveryErrors.AlreadyFlagged);
		}

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
		AnomalyTypeValue = type;
		AnomalyReason = reason;
		RaiseDomainEvent(new DeliveryFlaggedAnomalousDomainEvent(DeliveryId, type, reason, DistrictId));
		return Result.Success();
	}

	public Result MarkCancelled(DateTime timestamp, string reason)
	{
		var terminalCheck = EnsureNotTerminal();
		if (terminalCheck.IsFailure)
		{
			return terminalCheck;
		}

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

		// A delivery cancelled at or after its promised ETA is a service anomaly:
		// flag it so it surfaces on the dashboard and gets an AI dispatcher note via
		// the anomaly pipeline. Status stays Cancelled (terminal) — we do not move to
		// Anomalous; the flag + the raised event carry the anomaly signal.
		if (ExpectedEta.HasValue && timestamp >= ExpectedEta.Value)
		{
			var anomalyReason = $"Cancelled after ETA — {reason}";
			AnomalyFlag = true;
			AnomalyTypeValue = AnomalyType.EtaExceeded;
			AnomalyReason = anomalyReason;
			RaiseDomainEvent(new DeliveryFlaggedAnomalousDomainEvent(
				DeliveryId, AnomalyType.EtaExceeded, anomalyReason, DistrictId));
		}

		return Result.Success();
	}

	public Result SetRoute(double distanceMeters, double durationSeconds, decimal cost)
	{
		if (distanceMeters < 0 || durationSeconds < 0 || cost < 0)
			return Result.Failure(DeliveryErrors.InvalidRoute);

		RouteDistanceMeters = distanceMeters;
		RouteDurationSeconds = durationSeconds;
		RouteCost = cost;
		return Result.Success();
	}

	public Result SetUrgencyScore(int score, DateTime scoredAt)
	{
		if (score is < 1 or > 10)
			return Result.Failure(DeliveryErrors.InvalidUrgencyScore);

		UrgencyScore = score;
		UrgencyScoreAt = scoredAt;
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