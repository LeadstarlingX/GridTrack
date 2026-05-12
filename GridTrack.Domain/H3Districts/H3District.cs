using GridTrack.Domain.Abstractions;
using NetTopologySuite.Geometries;

namespace GridTrack.Domain.H3Districts;

public sealed class H3District : BaseEntity
{
	private H3District()
	{
	}

	private H3District(string h3Index, Point centerPoint, Polygon boundaryPolygon, int resolution)
	{
		H3Index = h3Index;
		CenterPoint = centerPoint;
		BoundaryPolygon = boundaryPolygon;
		Resolution = resolution;
	}

	public string H3Index { get; private set; } = string.Empty;
	public Point CenterPoint { get; private set; } = null!;
	public Polygon BoundaryPolygon { get; private set; } = null!;
	public int Resolution { get; private set; }

	public static Result<H3District> Create(string h3Index, Point centerPoint, Polygon boundaryPolygon, int resolution)
	{
		if (string.IsNullOrWhiteSpace(h3Index))
		{
			return Result.Failure<H3District>(H3DistrictErrors.InvalidIndex);
		}

		if (centerPoint is null)
		{
			return Result.Failure<H3District>(H3DistrictErrors.InvalidCenterPoint);
		}

		if (boundaryPolygon is null)
		{
			return Result.Failure<H3District>(H3DistrictErrors.InvalidBoundary);
		}

		if (resolution <= 0)
		{
			return Result.Failure<H3District>(H3DistrictErrors.InvalidResolution);
		}

		return Result.Success(new H3District(h3Index, centerPoint, boundaryPolygon, resolution));
	}

	public Result<bool> Contains(Point location)
	{
		if (location is null)
		{
			return Result.Failure<bool>(H3DistrictErrors.InvalidLocation);
		}

		return Result.Success(BoundaryPolygon.Contains(location));
	}

	public Result<IReadOnlyList<Polygon>> GetNeighbors(int ringDistance)
	{
		if (ringDistance <= 0)
		{
			return Result.Failure<IReadOnlyList<Polygon>>(H3DistrictErrors.InvalidRingDistance);
		}

		var neighbors = new List<Polygon>();
		var stepResult = EstimateRingDistance();
		if (stepResult.IsFailure)
		{
			return Result.Failure<IReadOnlyList<Polygon>>(stepResult.Error);
		}

		var step = stepResult.Value;

		for (var ring = 1; ring <= ringDistance; ring++)
		{
			var ringBuffer = BoundaryPolygon.Buffer(step * ring);
			if (ringBuffer is Polygon polygon)
			{
				neighbors.Add(polygon);
			}
		}

		return Result.Success<IReadOnlyList<Polygon>>(neighbors);
	}

	public Result<Polygon> ExpandServiceArea(int maxRings)
	{
		if (maxRings <= 0)
		{
			return Result.Failure<Polygon>(H3DistrictErrors.InvalidRingDistance);
		}

		var stepResult = EstimateRingDistance();
		if (stepResult.IsFailure)
		{
			return Result.Failure<Polygon>(stepResult.Error);
		}

		var step = stepResult.Value;
		var expanded = BoundaryPolygon.Buffer(step * maxRings);

		if (expanded is not Polygon polygon)
		{
			return Result.Failure<Polygon>(H3DistrictErrors.InvalidExpansion);
		}

		return Result.Success(polygon);
	}

	private Result<double> EstimateRingDistance()
	{
		var envelope = BoundaryPolygon.EnvelopeInternal;
		var size = Math.Max(envelope.Width, envelope.Height);

		if (size <= 0)
		{
			return Result.Failure<double>(H3DistrictErrors.InvalidBoundarySize);
		}

		return Result.Success(size / Math.Max(1, Resolution * 2));
	}
}