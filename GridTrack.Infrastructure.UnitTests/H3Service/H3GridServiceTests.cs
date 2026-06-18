using GridTrack.Application.Errors;
using GridTrack.Domain.Abstractions;
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

    // Rio de Janeiro (Southern/Western hemisphere - tests negative coordinates)
    private static readonly Point Rio = Factory.CreatePoint(new Coordinate(-43.1729, -22.9068));
    // London (Prime meridian, slight negative latitude)
    private static readonly Point London = Factory.CreatePoint(new Coordinate(-0.1276, 51.5074));

    [Test]
    public async Task GetCellAsync_NegativeCoordinates_Returns_Valid_Cell()
    {
        // If X/Y or Lat/Lng is swapped, negative coordinates usually produce cells
        // in the wrong hemisphere (e.g., ocean instead of land).
        var cell = (await _service.GetCellAsync(Rio, 8)).Value;
        await Assert.That(string.IsNullOrWhiteSpace(cell)).IsFalse();
        await Assert.That(cell.Length).IsEqualTo(15);
    }

    [Test]
    public async Task GetCellAsync_Resolution0_Returns_Valid_Cell()
    {
        // Res 0 is the coarsest (122 global cells). It's an edge case that can trigger edge-case math errors.
        var cell = (await _service.GetCellAsync(Damascus, 0)).Value;
        await Assert.That(string.IsNullOrWhiteSpace(cell)).IsFalse();
    }

    [Test]
    public async Task GetCellAsync_Resolution15_Returns_Valid_Cell()
    {
        // Res 15 is the finest (cm-level). It's an edge case for maximum index values.
        var cell = (await _service.GetCellAsync(Damascus, 15)).Value;
        await Assert.That(string.IsNullOrWhiteSpace(cell)).IsFalse();
    }

    // ── GetGridDiskAsync - Subtle Bug Catchers ─────────────────────

    [Test]
    public async Task GetGridDiskAsync_NullIndex_Returns_Failure()
    {
        // Ensure nulls are handled as cleanly as empty strings
        var result = await _service.GetGridDiskAsync(null!, 1);
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3ServiceErrors.InvalidCellIndex);
    }

    // ── FillBoundingBoxAsync - Subtle Bug Catchers ─────────────────

    [Test]
    public async Task FillBoundingBoxAsync_PointBoundingBox_Returns_OneCell()
    {
        // A bbox where min == max is a single point. With Intersects mode, it should return exactly 1 cell.
        var cells = await _service.FillBoundingBoxAsync(33.5138, 33.5138, 36.2765, 36.2765, 8);
        await Assert.That(cells.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task FillBoundingBoxAsync_InvertedBoundingBox_Returns_SameAsNormal()
    {
        // If a user accidentally passes maxLat as minLat, NTS Envelope auto-corrects it.
        // This test ensures our service doesn't crash and returns the same data.
        var normal = (await _service.FillBoundingBoxAsync(33.40, 33.60, 36.20, 36.40, 8)).ToList();
        var inverted = (await _service.FillBoundingBoxAsync(33.60, 33.40, 36.40, 36.20, 8)).ToList();

        await Assert.That(inverted.Count).IsEqualTo(normal.Count);
    }

    [Test]
    public async Task FillBoundingBoxAsync_SouthernHemisphere_CellFromPoint_Should_Be_In_Result()
    {
        // This is the ULTIMATE test for Lat/Lng swap bugs.
        // If X and Y are swapped, Rio's cell will end up in the Northern Hemisphere and fail.
        var cell = (await _service.GetCellAsync(Rio, 8)).Value;
        var cells = await _service.FillBoundingBoxAsync(-23.00, -22.80, -43.30, -43.00, 8);

        await Assert.That(cells).Contains(cell);
    }

    [Test]
    public async Task FillBoundingBoxAsync_OutsideCell_IsNot_In_Result()
    {
        // Ensures the bounding box isn't just returning random global cells
        var aleppoCell = (await _service.GetCellAsync(Aleppo, 8)).Value;
        var damascusBoxCells = await _service.FillBoundingBoxAsync(33.40, 33.60, 36.20, 36.40, 8);

        await Assert.That(damascusBoxCells).DoesNotContain(aleppoCell);
    }

    [Test]
    public async Task FillBoundingBoxAsync_LargerResolution_Returns_SmallerArea_Per_Cell()
    {
        // Ensures that while Res 9 returns MORE cells, each Res 9 cell string represents a smaller area.
        // The H3 index structure embeds the resolution in the string.
        // Res 8 strings start with '88', Res 9 strings start with '89'.
        var res8 = (await _service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8)).ToList();
        var res9 = (await _service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 9)).ToList();

        await Assert.That(res8.First().StartsWith("88")).IsTrue();
        await Assert.That(res9.First().StartsWith("89")).IsTrue();
    }

    [Test]
    public async Task GetCellAsync_Should_Return_NonEmpty_String()
    {
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        await Assert.That(string.IsNullOrWhiteSpace(cell)).IsFalse();
    }

    [Test]
    public async Task GetCellAsync_Should_Return_Valid_H3_Hex_String()
    {
        // H3 cell strings are 15-character lowercase hex
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        await Assert.That(cell).Length().EqualTo(15);
        await Assert.That(cell.All(c => "0123456789abcdef".Contains(c))).IsTrue();
    }

    [Test]
    public async Task GetCellAsync_SamePoint_SameResolution_Returns_SameCell()
    {
        var cell1 = (await _service.GetCellAsync(Damascus, 8)).Value;
        var cell2 = (await _service.GetCellAsync(Damascus, 8)).Value;
        await Assert.That(cell1).IsEqualTo(cell2);
    }

    [Test]
    public async Task GetCellAsync_SamePoint_DifferentResolution_Returns_DifferentCell()
    {
        var cell8 = (await _service.GetCellAsync(Damascus, 8)).Value;
        var cell9 = (await _service.GetCellAsync(Damascus, 9)).Value;
        await Assert.That(cell8).IsNotEqualTo(cell9);
    }

    [Test]
    public async Task GetCellAsync_NullPoint_Returns_Failure()
    {
        var result = await _service.GetCellAsync(null!, 8);
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3ServiceErrors.LocationNotProvided);
    }

    [Test]
    public async Task GetCellAsync_VeryClosePoints_LowResolution_Return_SameCell()
    {
        // Two points ~5m apart should map to the same cell at resolution 8
        var p1 = Factory.CreatePoint(new Coordinate(36.2765, 33.5138));
        var p2 = Factory.CreatePoint(new Coordinate(36.2766, 33.5138));

        var cell1 = (await _service.GetCellAsync(p1, 5)).Value; // resolution 5 = ~8km cells
        var cell2 = (await _service.GetCellAsync(p2, 5)).Value;
        await Assert.That(cell1).IsEqualTo(cell2);
    }

    [Test]
    public async Task GetCellAsync_DistantPoints_Return_DifferentCells()
    {
        var damascusCell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var aleppoCell = (await _service.GetCellAsync(Aleppo, 8)).Value;
        await Assert.That(damascusCell).IsNotEqualTo(aleppoCell);
    }

    // ── GetGridDiskAsync ──────────────────────────────────────────

    [Test]
    public async Task GetGridDiskAsync_Ring1_Returns_Exactly_6_Cells()
    {
        // H3 hexagonal grid: ring 1 = 6 neighbors (origin excluded)
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var neighbors = (await _service.GetGridDiskAsync(cell, 1)).Value.ToList();
        await Assert.That(neighbors.Count).IsEqualTo(6);
    }

    [Test]
    public async Task GetGridDiskAsync_Ring2_Returns_Exactly_18_Cells()
    {
        // Ring 1 = 6, Ring 2 adds 12 more = 18 total (origin excluded)
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var neighbors = (await _service.GetGridDiskAsync(cell, 2)).Value.ToList();
        await Assert.That(neighbors.Count).IsEqualTo(18);
    }

    [Test]
    public async Task GetGridDiskAsync_DoesNot_Contain_OriginCell()
    {
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var neighbors = (await _service.GetGridDiskAsync(cell, 1)).Value.ToList();
        await Assert.That(neighbors).DoesNotContain(cell);
    }

    [Test]
    public async Task GetGridDiskAsync_Ring2_Contains_More_Than_Ring1()
    {
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var ring1 = (await _service.GetGridDiskAsync(cell, 1)).Value.ToList();
        var ring2 = (await _service.GetGridDiskAsync(cell, 2)).Value.ToList();
        await Assert.That(ring2.Count).IsGreaterThan(ring1.Count);
    }

    [Test]
    public async Task GetGridDiskAsync_All_Returned_Cells_Are_Valid_H3_Strings()
    {
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var neighbors = (await _service.GetGridDiskAsync(cell, 1)).Value;

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
        var originCell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var ring1 = (await _service.GetGridDiskAsync(originCell, 1)).Value.ToList();
        var firstNeighbor = ring1.First();

        var neighborRing1 = (await _service.GetGridDiskAsync(firstNeighbor, 1)).Value.ToList();
        await Assert.That(neighborRing1).Contains(originCell);
    }

    [Test]
    public async Task GetGridDiskAsync_EmptyIndex_Returns_Failure()
    {
        var result = await _service.GetGridDiskAsync("", 1);
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3ServiceErrors.InvalidCellIndex);
    }

    [Test]
    public async Task GetGridDiskAsync_ZeroRing_Returns_Failure()
    {
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var result = await _service.GetGridDiskAsync(cell, 0);
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3ServiceErrors.InvalidRingDistance);
    }

    [Test]
    public async Task GetGridDiskAsync_NegativeRing_Returns_Failure()
    {
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var result = await _service.GetGridDiskAsync(cell, -1);
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(H3ServiceErrors.InvalidRingDistance);
    }

    [Test]
    public async Task GetGridDiskAsync_DistantCells_Not_In_SmallDisk()
    {
        var damascusCell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var aleppoCell = (await _service.GetCellAsync(Aleppo, 8)).Value;
        var neighbors = (await _service.GetGridDiskAsync(damascusCell, 10)).Value;
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
        var cell = (await _service.GetCellAsync(Damascus, 8)).Value;
        var cells = await _service.FillBoundingBoxAsync(33.40, 33.60, 36.20, 36.40, 8);

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
