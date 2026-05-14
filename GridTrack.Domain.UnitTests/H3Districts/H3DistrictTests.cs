using GridTrack.Domain.H3Districts;
using NetTopologySuite.Geometries;

namespace GridTrack.Domain.UnitTests.H3Districts;

public class H3DistrictTests
{
    private static readonly GeometryFactory Factory = new();

    [Test]
    public async Task Create_Should_Return_Success()
    {
        var polygon = CreateSquare();
        var result = H3District.Create("h3-1", Factory.CreatePoint(new Coordinate(0.5, 0.5)), polygon, 9);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Contains_Should_Return_True_For_Point_Inside()
    {
        var district = CreateDistrict();

        var result = district.Contains(Factory.CreatePoint(new Coordinate(0.5, 0.5)));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();
    }

    [Test]
    public async Task GetNeighbors_Should_Fail_When_Resolved_In_Infrastructure()
    {
        var district = CreateDistrict();

        var result = district.GetNeighbors(2);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.NeighborsResolvedInInfrastructure);
    }

    [Test]
    public async Task ExpandServiceArea_Should_Return_Polygon()
    {
        var district = CreateDistrict();

        var result = district.ExpandServiceArea(2);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
    }

    [Test]
    public async Task GetNeighbors_Should_Fail_For_Invalid_Distance()
    {
        var district = CreateDistrict();

        var result = district.GetNeighbors(0);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.InvalidRingDistance);
    }

    [Test]
    public async Task Create_Should_Fail_For_Invalid_Resolution()
    {
        var polygon = CreateSquare();

        var result = H3District.Create("h3-1", Factory.CreatePoint(new Coordinate(0.5, 0.5)), polygon, 0);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.InvalidResolution);
    }

    [Test]
    public async Task ExpandServiceArea_Should_Fail_For_Invalid_Rings()
    {
        var district = CreateDistrict();

        var result = district.ExpandServiceArea(0);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.InvalidRingDistance);
    }


    [Test]
    public async Task Create_Should_Fail_When_H3Index_Is_Empty()
    {
        var polygon = CreateSquare();

        var result = H3District.Create("", Factory.CreatePoint(new Coordinate(0.5, 0.5)), polygon, 9);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.InvalidIndex);
    }

    [Test]
    public async Task Create_Should_Fail_When_CenterPoint_Is_Null()
    {
        var polygon = CreateSquare();

        var result = H3District.Create("h3-1", null!, polygon, 9);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.InvalidCenterPoint);
    }

    [Test]
    public async Task Create_Should_Fail_When_Boundary_Is_Null()
    {
        var result = H3District.Create("h3-1", Factory.CreatePoint(new Coordinate(0.5, 0.5)), null!, 9);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.InvalidBoundary);
    }

    [Test]
    public async Task Contains_Should_Fail_When_Location_Is_Null()
    {
        var district = CreateDistrict();

        var result = district.Contains(null!);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.InvalidLocation);
    }

    [Test]
    public async Task Contains_Should_Return_False_For_Point_Outside()
    {
        var district = CreateDistrict();

        var result = district.Contains(Factory.CreatePoint(new Coordinate(5, 5)));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();
    }

    [Test]
    public async Task GetNeighbors_Should_Fail_For_Negative_Distance()
    {
        var district = CreateDistrict();

        var result = district.GetNeighbors(-1);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.InvalidRingDistance);
    }

    [Test]
    public async Task ExpandServiceArea_Should_Fail_For_Negative_Rings()
    {
        var district = CreateDistrict();

        var result = district.ExpandServiceArea(-1);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3DistrictErrors.InvalidRingDistance);
    }

    [Test]
    public async Task ExpandServiceArea_With_Large_RingCount_Should_Succeed()
    {
        var district = CreateDistrict();

        var result = district.ExpandServiceArea(10);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Area).IsGreaterThan(district.BoundaryPolygon.Area);
    }
    
    
    private static H3District CreateDistrict()
    {
        var polygon = CreateSquare();
        var result = H3District.Create("h3-1", Factory.CreatePoint(new Coordinate(0.5, 0.5)), polygon, 9);
        return result.Value;
    }

    private static Polygon CreateSquare()
    {
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(0, 1),
            new Coordinate(1, 1),
            new Coordinate(1, 0),
            new Coordinate(0, 0)
        };

        return Factory.CreatePolygon(coords);
    }
}
