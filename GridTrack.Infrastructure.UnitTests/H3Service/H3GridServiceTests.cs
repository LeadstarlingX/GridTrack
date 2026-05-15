using GridTrack.Infrastructure.H3Service;
using H3;
using H3.Extensions;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.UnitTests.H3Service;

public class H3GridServiceTests
{
    private static readonly GeometryFactory Factory = new();
    private readonly H3GridService _service = new();
 
    // Damascus city center (lng=36.2765, lat=33.5138)
    private static readonly Point Damascus = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
    // Aleppo (~350km from Damascus)
    private static readonly Point Aleppo = Factory.CreatePoint(new Coordinate(37.1611, 36.2021));
    
    [Test]
    public async Task GetCellAsync_Should_Return_NonEmpty_String()
    {
        var cell = await _service.GetCellAsync(Damascus, 8);
        await Assert.That(string.IsNullOrWhiteSpace(cell)).IsFalse();
    }
 
    [Test]
    public async Task GetCellAsync_Should_Return_Valid_H3_Hex_String()
    {
        // H3 cell strings are 15-character lowercase hex
        var cell = await _service.GetCellAsync(Damascus, 8);
        await Assert.That(cell).HasLength().EqualTo(15);
        await Assert.That(cell.All(c => "0123456789abcdef".Contains(c))).IsTrue();
    }
 
    [Test]
    public async Task GetCellAsync_SamePoint_SameResolution_Returns_SameCell()
    {
        var cell1 = await _service.GetCellAsync(Damascus, 8);
        var cell2 = await _service.GetCellAsync(Damascus, 8);
        await Assert.That(cell1).IsEqualTo(cell2);
    }
 
    [Test]
    public async Task GetCellAsync_SamePoint_DifferentResolution_Returns_DifferentCell()
    {
        var cell8 = await _service.GetCellAsync(Damascus, 8);
        var cell9 = await _service.GetCellAsync(Damascus, 9);
        await Assert.That(cell8).IsNotEqualTo(cell9);
    }
 
    [Test]
    public async Task GetCellAsync_NullPoint_Throws_ArgumentNullException()
    {
        await Assert.That(async () => await _service.GetCellAsync(null!, 8))
            .Throws<ArgumentNullException>();
    }
 
    [Test]
    public async Task GetCellAsync_VeryClosePoints_LowResolution_Return_SameCell()
    {
        // Two points ~5m apart should map to the same cell at resolution 8
        var p1 = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var p2 = Factory.CreatePoint(new Coordinate(36.2766, 33.5138));
 
        var cell1 = await _service.GetCellAsync(p1, 5); // resolution 5 = ~8km cells
        var cell2 = await _service.GetCellAsync(p2, 5);
        await Assert.That(cell1).IsEqualTo(cell2);
    }
 
    [Test]
    public async Task GetCellAsync_DistantPoints_Return_DifferentCells()
    {
        var damascusCell = await _service.GetCellAsync(Damascus, 8);
        var aleppoCell = await _service.GetCellAsync(Aleppo, 8);
        await Assert.That(damascusCell).IsNotEqualTo(aleppoCell);
    }
 
    // ── GetGridDiskAsync ──────────────────────────────────────────
 
    [Test]
    public async Task GetGridDiskAsync_Ring1_Returns_Exactly_6_Cells()
    {
        // H3 hexagonal grid: ring 1 = 6 neighbors (origin excluded)
        var cell = await _service.GetCellAsync(Damascus, 8);
        var neighbors = (await _service.GetGridDiskAsync(cell, 1)).ToList();
        await Assert.That(neighbors.Count).IsEqualTo(6);
    }
 
    [Test]
    public async Task GetGridDiskAsync_Ring2_Returns_Exactly_18_Cells()
    {
        // Ring 1 = 6, Ring 2 adds 12 more = 18 total (origin excluded)
        var cell = await _service.GetCellAsync(Damascus, 8);
        var neighbors = (await _service.GetGridDiskAsync(cell, 2)).ToList();
        await Assert.That(neighbors.Count).IsEqualTo(18);
    }
 
    [Test]
    public async Task GetGridDiskAsync_DoesNot_Contain_OriginCell()
    {
        var cell = await _service.GetCellAsync(Damascus, 8);
        var neighbors = (await _service.GetGridDiskAsync(cell, 1)).ToList();
        await Assert.That(neighbors).DoesNotContain(cell);
    }
 
    [Test]
    public async Task GetGridDiskAsync_Ring2_Contains_More_Than_Ring1()
    {
        var cell = await _service.GetCellAsync(Damascus, 8);
        var ring1 = (await _service.GetGridDiskAsync(cell, 1)).ToList();
        var ring2 = (await _service.GetGridDiskAsync(cell, 2)).ToList();
        await Assert.That(ring2.Count).IsGreaterThan(ring1.Count);
    }
 
    [Test]
    public async Task GetGridDiskAsync_All_Returned_Cells_Are_Valid_H3_Strings()
    {
        var cell = await _service.GetCellAsync(Damascus, 8);
        var neighbors = await _service.GetGridDiskAsync(cell, 1);
 
        foreach (var neighbor in neighbors)
        {
            await Assert.That(string.IsNullOrWhiteSpace(neighbor)).IsFalse();
            await Assert.That(neighbor.Length).IsEqualTo(15);
        }
    }
 
    [Test]
    public async Task GetGridDiskAsync_IsSymmetric_NeighborContainsOriginInItsRing()
    {
        // If B is in A's ring 1, then A should be in B's ring 1
        var originCell = await _service.GetCellAsync(Damascus, 8);
        var ring1 = (await _service.GetGridDiskAsync(originCell, 1)).ToList();
        var firstNeighbor = ring1.First();
 
        var neighborRing1 = (await _service.GetGridDiskAsync(firstNeighbor, 1)).ToList();
        await Assert.That(neighborRing1).Contains(originCell);
    }
 
    [Test]
    public async Task GetGridDiskAsync_EmptyIndex_Throws_ArgumentException()
    {
        await Assert.That(async () => await _service.GetGridDiskAsync("", 1))
            .Throws<ArgumentException>();
    }
 
    [Test]
    public async Task GetGridDiskAsync_ZeroRing_Throws_ArgumentOutOfRangeException()
    {
        var cell = await _service.GetCellAsync(Damascus, 8);
        await Assert.That(async () => await _service.GetGridDiskAsync(cell, 0))
            .Throws<ArgumentOutOfRangeException>();
    }
 
    [Test]
    public async Task GetGridDiskAsync_NegativeRing_Throws_ArgumentOutOfRangeException()
    {
        var cell = await _service.GetCellAsync(Damascus, 8);
        await Assert.That(async () => await _service.GetGridDiskAsync(cell, -1))
            .Throws<ArgumentOutOfRangeException>();
    }
 
    [Test]
    public async Task GetGridDiskAsync_DistantCells_Not_In_SmallDisk()
    {
        var damascusCell = await _service.GetCellAsync(Damascus, 8);
        var aleppoCell = await _service.GetCellAsync(Aleppo, 8);
        var neighbors = await _service.GetGridDiskAsync(damascusCell, 10);
        await Assert.That(neighbors).DoesNotContain(aleppoCell);
    }
 
    // ── FillBoundingBoxAsync ──────────────────────────────────────
 
    [Test]
    public async Task FillBoundingBoxAsync_Returns_NonEmpty_Cells()
    {
        var cells = await _service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8);
        await Assert.That(cells.Any()).IsTrue();
    }
 
    [Test]
    public async Task FillBoundingBoxAsync_All_Cells_Are_Valid_H3_Strings()
    {
        var cells = await _service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8);
        foreach (var cell in cells)
        {
            await Assert.That(string.IsNullOrWhiteSpace(cell)).IsFalse();
            await Assert.That(cell.Length).IsEqualTo(15);
        }
    }
 
    [Test]
    public async Task FillBoundingBoxAsync_LargerArea_Returns_MoreCells()
    {
        var small = (await _service.FillBoundingBoxAsync(33.50, 33.51, 36.27, 36.28, 8)).ToList();
        var large = (await _service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8)).ToList();
        await Assert.That(large.Count).IsGreaterThan(small.Count);
    }
 
    [Test]
    public async Task FillBoundingBoxAsync_HigherResolution_Returns_MoreCells()
    {
        var res8 = (await _service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8)).ToList();
        var res9 = (await _service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 9)).ToList();
        await Assert.That(res9.Count).IsGreaterThan(res8.Count);
    }
 
    [Test]
    public async Task FillBoundingBoxAsync_CellFromPoint_Should_Be_In_BoundingBox_Result()
    {
        // A cell computed from a point inside the bbox should appear in the fill results
        var cell = await _service.GetCellAsync(Damascus, 8);
        var cells = await _service.FillBoundingBoxAsync(33.40, 33.60, 36.20, 36.40, 8);
        
        var index = new H3Index(cells.First());
        var c = index.ToCoordinate();
        
        await Assert.That(cells).Contains(cell);
    }
 
    [Test]
    public async Task FillBoundingBoxAsync_NoDuplicates()
    {
        var cells = (await _service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8)).ToList();
        var distinct = cells.Distinct().ToList();
        await Assert.That(cells.Count).IsEqualTo(distinct.Count);
    }
 
    
}
