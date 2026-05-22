using FluentAssertions;
using GridTrack.Application.Interfaces;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace GridTrack.IntegrationTests.H3ServiceTests;

public class H3GridServiceIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);

    // ── Fixed coordinates ─────────────────────────────────────────────────
    // Damascus city center (lng=36.2765, lat=33.5138)
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));
    // Aleppo (~350km from Damascus)
    private static Point Aleppo => GeoFactory.CreatePoint(new Coordinate(37.1611, 36.2021));
    // Rio de Janeiro (Southern/Western hemisphere - tests negative coordinates)
    private static Point Rio => GeoFactory.CreatePoint(new Coordinate(-43.1729, -22.9068));
    // London (Prime meridian)
    private static Point London => GeoFactory.CreatePoint(new Coordinate(-0.1276, 51.5074));

    // ── Helpers ───────────────────────────────────────────────────────────

    private static IH3GridService GetH3Service()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IH3GridService>();
    }

    // ── GetCellAsync Integration Tests ────────────────────────────────────

    [Test]
    [NotInParallel(Order = 50)]
    public async Task GetCellAsync_Damascus_Should_Return_Valid_H3_Cell()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Damascus, 8);

        cell.Should().NotBeNullOrWhiteSpace();
        cell.Should().HaveLength(15);
        cell.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Test]
    [NotInParallel(Order = 51)]
    public async Task GetCellAsync_NegativeCoordinates_Should_Return_Valid_Cell()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Rio, 8);

        cell.Should().NotBeNullOrWhiteSpace();
        cell.Should().HaveLength(15);
    }

    [Test]
    [NotInParallel(Order = 52)]
    public async Task GetCellAsync_SamePoint_SameResolution_Should_Return_Consistent_Results()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell1 = await service.GetCellAsync(Damascus, 8);
        var cell2 = await service.GetCellAsync(Damascus, 8);

        cell1.Should().Be(cell2);
    }

    [Test]
    [NotInParallel(Order = 53)]
    public async Task GetCellAsync_SamePoint_DifferentResolution_Should_Return_Different_Cells()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell8 = await service.GetCellAsync(Damascus, 8);
        var cell9 = await service.GetCellAsync(Damascus, 9);

        cell8.Should().NotBe(cell9);
    }

    [Test]
    [NotInParallel(Order = 54)]
    public async Task GetCellAsync_DistantPoints_Should_Return_Different_Cells()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var damascusCell = await service.GetCellAsync(Damascus, 8);
        var aleppoCell = await service.GetCellAsync(Aleppo, 8);

        damascusCell.Should().NotBe(aleppoCell);
    }

    [Test]
    [NotInParallel(Order = 55)]
    public async Task GetCellAsync_Resolution0_Should_Return_Valid_Cell()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Damascus, 0);

        cell.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [NotInParallel(Order = 56)]
    public async Task GetCellAsync_Resolution15_Should_Return_Valid_Cell()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Damascus, 15);

        cell.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [NotInParallel(Order = 57)]
    public async Task GetCellAsync_NullPoint_Should_Throw_ArgumentNullException()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();

        await Assert.That(async () => await service.GetCellAsync(null!, 8))
            .Throws<ArgumentNullException>();
    }

    // ── GetGridDiskAsync Integration Tests ────────────────────────────────

    [Test]
    [NotInParallel(Order = 58)]
    public async Task GetGridDiskAsync_Ring1_Should_Return_Exactly_6_Cells()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Damascus, 8);
        var neighbors = (await service.GetGridDiskAsync(cell, 1)).ToList();

        neighbors.Should().HaveCount(6);
    }

    [Test]
    [NotInParallel(Order = 59)]
    public async Task GetGridDiskAsync_Ring2_Should_Return_Exactly_18_Cells()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Damascus, 8);
        var neighbors = (await service.GetGridDiskAsync(cell, 2)).ToList();

        neighbors.Should().HaveCount(18);
    }

    [Test]
    [NotInParallel(Order = 60)]
    public async Task GetGridDiskAsync_Should_Not_Contain_Origin_Cell()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var originCell = await service.GetCellAsync(Damascus, 8);
        var neighbors = (await service.GetGridDiskAsync(originCell, 1)).ToList();

        neighbors.Should().NotContain(originCell);
    }

    [Test]
    [NotInParallel(Order = 61)]
    public async Task GetGridDiskAsync_All_Returned_Cells_Should_Be_Valid_H3_Strings()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Damascus, 8);
        var neighbors = await service.GetGridDiskAsync(cell, 1);

        foreach (var neighbor in neighbors)
        {
            neighbor.Should().NotBeNullOrWhiteSpace();
            neighbor.Should().HaveLength(15);
            neighbor.Should().MatchRegex("^[0-9a-f]+$");
        }
    }

    [Test]
    [NotInParallel(Order = 62)]
    public async Task GetGridDiskAsync_EmptyIndex_Should_Throw_ArgumentException()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();

        await Assert.That(async () => await service.GetGridDiskAsync("", 1))
            .Throws<ArgumentException>();
    }

    [Test]
    [NotInParallel(Order = 63)]
    public async Task GetGridDiskAsync_ZeroRing_Should_Throw_ArgumentOutOfRangeException()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Damascus, 8);

        await Assert.That(async () => await service.GetGridDiskAsync(cell, 0))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [NotInParallel(Order = 64)]
    public async Task GetGridDiskAsync_NegativeRing_Should_Throw_ArgumentOutOfRangeException()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Damascus, 8);

        await Assert.That(async () => await service.GetGridDiskAsync(cell, -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [NotInParallel(Order = 65)]
    public async Task GetGridDiskAsync_IsSymmetric_Neighbor_Should_Contain_Origin_In_Its_Ring()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var originCell = await service.GetCellAsync(Damascus, 8);
        var ring1 = (await service.GetGridDiskAsync(originCell, 1)).ToList();
        var firstNeighbor = ring1.First();

        var neighborRing1 = (await service.GetGridDiskAsync(firstNeighbor, 1)).ToList();
        neighborRing1.Should().Contain(originCell);
    }

    // ── FillBoundingBoxAsync Integration Tests ────────────────────────────

    [Test]
    [NotInParallel(Order = 66)]
    public async Task FillBoundingBoxAsync_Damascus_Area_Should_Return_NonEmpty_Cells()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cells = await service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8);

        cells.Should().NotBeEmpty();
    }

    [Test]
    [NotInParallel(Order = 67)]
    public async Task FillBoundingBoxAsync_All_Cells_Should_Be_Valid_H3_Strings()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cells = await service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8);

        foreach (var cell in cells)
        {
            cell.Should().NotBeNullOrWhiteSpace();
            cell.Should().HaveLength(15);
            cell.Should().MatchRegex("^[0-9a-f]+$");
        }
    }

    [Test]
    [NotInParallel(Order = 68)]
    public async Task FillBoundingBoxAsync_LargerArea_Should_Return_MoreCells()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var small = (await service.FillBoundingBoxAsync(33.50, 33.51, 36.27, 36.28, 8)).ToList();
        var large = (await service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8)).ToList();

        large.Count.Should().BeGreaterThan(small.Count);
    }

    [Test]
    [NotInParallel(Order = 69)]
    public async Task FillBoundingBoxAsync_HigherResolution_Should_Return_MoreCells()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var res8 = (await service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8)).ToList();
        var res9 = (await service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 9)).ToList();

        res9.Count.Should().BeGreaterThan(res8.Count);
    }

    [Test]
    [NotInParallel(Order = 70)]
    public async Task FillBoundingBoxAsync_CellFromPoint_Should_Be_In_BoundingBox_Result()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Damascus, 8);
        var cells = await service.FillBoundingBoxAsync(33.40, 33.60, 36.20, 36.40, 8);

        cells.Should().Contain(cell);
    }

    [Test]
    [NotInParallel(Order = 71)]
    public async Task FillBoundingBoxAsync_NoDuplicates()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cells = (await service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8)).ToList();
        var distinct = cells.Distinct().ToList();

        cells.Count.Should().Be(distinct.Count);
    }

    [Test]
    [NotInParallel(Order = 72)]
    public async Task FillBoundingBoxAsync_PointBoundingBox_Should_Return_OneCell()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cells = await service.FillBoundingBoxAsync(33.5138, 33.5138, 36.2765, 36.2765, 8);

        cells.Should().HaveCount(1);
    }

    [Test]
    [NotInParallel(Order = 73)]
    public async Task FillBoundingBoxAsync_SouthernHemisphere_Should_Work_Correctly()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var cell = await service.GetCellAsync(Rio, 8);
        var cells = await service.FillBoundingBoxAsync(-23.00, -22.80, -43.30, -43.00, 8);

        cells.Should().Contain(cell);
    }

    [Test]
    [NotInParallel(Order = 74)]
    public async Task FillBoundingBoxAsync_OutsideCell_Should_Not_Be_In_Result()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var aleppoCell = await service.GetCellAsync(Aleppo, 8);
        var damascusBoxCells = await service.FillBoundingBoxAsync(33.40, 33.60, 36.20, 36.40, 8);

        damascusBoxCells.Should().NotContain(aleppoCell);
    }

    [Test]
    [NotInParallel(Order = 75)]
    public async Task FillBoundingBoxAsync_Performance_WithLargeArea_Should_Be_Reasonable()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var cells = (await service.FillBoundingBoxAsync(33.00, 34.00, 35.00, 37.00, 8)).ToList();
        stopwatch.Stop();

        cells.Should().NotBeEmpty();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
    }

    [Test]
    [NotInParallel(Order = 76)]
    public async Task FillBoundingBoxAsync_ResolutionString_Prefix_Should_Match_Resolution()
    {
        await ResetDatabaseAsync();

        var service = GetH3Service();
        var res8 = (await service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 8)).ToList();
        var res9 = (await service.FillBoundingBoxAsync(33.50, 33.52, 36.27, 36.29, 9)).ToList();

        res8.First().Should().StartWith("88");
        res9.First().Should().StartWith("89");
    }
}