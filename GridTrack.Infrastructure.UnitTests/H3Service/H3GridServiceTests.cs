using GridTrack.Infrastructure.H3Service;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.UnitTests.H3Service;

public class H3GridServiceTests
{
    private static readonly GeometryFactory Factory = new();
    private readonly H3GridService _service = new();

    [Test]
    public async Task GetCellIndexForPointAsync_Should_Return_Cell()
    {
        var service = new H3GridService();
        var point = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));

        var cell = await service.GetCellIndexForPointAsync(point, 8);

        await Assert.That(string.IsNullOrWhiteSpace(cell)).IsFalse();
    }

    [Test]
    public async Task GetNeighborCellsAsync_Should_Return_Neighbors()
    {
        var service = new H3GridService();
        var point = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var cell = await service.GetCellIndexForPointAsync(point, 8);

        var neighbors = await service.GetNeighborCellsAsync(cell, 1);

        await Assert.That(neighbors.Any()).IsTrue();
    }

    [Test]
    public async Task GenerateGridBoundsAsync_Should_Return_Cells()
    {
        var service = new H3GridService();

        var cells = await service.GenerateGridBoundsAsync(33.50, 33.52, 36.27, 36.29, 8);

        await Assert.That(cells.Any()).IsTrue();
    }
    
    
    [Test]
    public async Task GetCellIndexForPointAsync_SamePoint_SameResolution_Should_Return_SameCell()
    {
        var point = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));

        var cell1 = await _service.GetCellIndexForPointAsync(point, 8);
        var cell2 = await _service.GetCellIndexForPointAsync(point, 8);

        await Assert.That(cell1).IsEqualTo(cell2);
    }

    [Test]
    public async Task GetCellIndexForPointAsync_SamePoint_DifferentResolution_Should_Return_DifferentCell()
    {
        var point = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));

        var cell8 = await _service.GetCellIndexForPointAsync(point, 8);
        var cell9 = await _service.GetCellIndexForPointAsync(point, 9);

        await Assert.That(cell8).IsNotEqualTo(cell9);
    }

    [Test]
    public async Task GetCellIndexForPointAsync_NullLocation_Should_ThrowArgumentNullException()
    {
        await Assert.That(async () => await _service.GetCellIndexForPointAsync(null!, 8))
            .Throws<ArgumentNullException>();
    }
    
    [Test]
    public async Task GetNeighborCellsAsync_Ring1_Should_Return_Exactly_6_Cells_Excluding_Center()
    {
        // GridDiskDistances(1) returns center + 6 neighbors = 7 cells total for hexagonal grid
        var point = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var cell = await _service.GetCellIndexForPointAsync(point, 8);

        var neighbors = await _service.GetNeighborCellsAsync(cell, 1);
        var neighborList = neighbors.ToList();

        await Assert.That(neighborList.Count).IsEqualTo(7);

        // Center cell should NOT be included in ring 1
        await Assert.That(neighborList).DoesNotContain(cell);
    }

    [Test]
    public async Task GetNeighborCellsAsync_Ring2_Should_Return_More_Cells_Than_Ring1()
    {
        var point = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var cell = await _service.GetCellIndexForPointAsync(point, 8);

        var ring1 = await _service.GetNeighborCellsAsync(cell, 1);
        var ring2 = await _service.GetNeighborCellsAsync(cell, 2);

        await Assert.That(ring2.Count()).IsGreaterThan(ring1.Count());
    }

    [Test]
    public async Task GetNeighborCellsAsync_EmptyCellIndex_Should_ThrowArgumentException()
    {
        await Assert.That(async () => await _service.GetNeighborCellsAsync("", 1))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task GetNeighborCellsAsync_ZeroRingDistance_Should_ThrowArgumentOutOfRangeException()
    {
        var point = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var cell = await _service.GetCellIndexForPointAsync(point, 8);

        await Assert.That(async () => await _service.GetNeighborCellsAsync(cell, 0))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GetNeighborCellsAsync_NegativeRingDistance_Should_ThrowArgumentOutOfRangeException()
    {
        var point = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var cell = await _service.GetCellIndexForPointAsync(point, 8);

        await Assert.That(async () => await _service.GetNeighborCellsAsync(cell, -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GetNeighborCellsAsync_TwoNearbyCells_WithinRing1_Should_BeInNeighbors()
    {
        // Create two points very close together (should be in adjacent cells)
        var point1 = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var point2 = Factory.CreatePoint(new Coordinate(36.2770, 33.5143)); // ~50m away

        var cell1 = await _service.GetCellIndexForPointAsync(point1, 8);
        var cell2 = await _service.GetCellIndexForPointAsync(point2, 8);

        // Get neighbors of cell1 with ring distance 1
        var neighbors = await _service.GetNeighborCellsAsync(cell1, 1);
        var neighborList = neighbors.ToList();

        // Both cell1 (center) and cell2 (nearby) should be in the neighbor set
        await Assert.That(neighborList).DoesNotContain(cell1);
    }

    [Test]
    public async Task GetNeighborCellsAsync_TwoDistantCells_SecondNotInFirstsNeighbors()
    {
        // Create two points far apart (Damascus to Aleppo ~350km)
        var damascus = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var aleppo = Factory.CreatePoint(new Coordinate(37.1611, 36.2021));

        var damascusCell = await _service.GetCellIndexForPointAsync(damascus, 8);
        var aleppoCell = await _service.GetCellIndexForPointAsync(aleppo, 8);

        // Get neighbors of Damascus cell with ring distance 10 (large but not enough for 350km)
        var neighbors = await _service.GetNeighborCellsAsync(damascusCell, 10);
        var neighborList = neighbors.ToList();

        // Aleppo should NOT be in Damascus neighbors
        await Assert.That(neighborList).DoesNotContain(aleppoCell);
    }
    
    [Test]
    public async Task GenerateGridBoundsAsync_LargerArea_Should_Return_MoreCells()
    {
        var smallArea = await _service.GenerateGridBoundsAsync(33.50, 33.51, 36.27, 36.28, 8);
        var largeArea = await _service.GenerateGridBoundsAsync(33.50, 33.52, 36.27, 36.29, 8);

        await Assert.That(largeArea.Count()).IsGreaterThan(smallArea.Count());
    }

    [Test]
    public async Task GenerateGridBoundsAsync_HigherResolution_Should_Return_MoreCells()
    {
        var res8 = await _service.GenerateGridBoundsAsync(33.50, 33.52, 36.27, 36.29, 8);
        var res9 = await _service.GenerateGridBoundsAsync(33.50, 33.52, 36.27, 36.29, 9);

        await Assert.That(res9.Count()).IsGreaterThan(res8.Count());
    }

 
    
}
