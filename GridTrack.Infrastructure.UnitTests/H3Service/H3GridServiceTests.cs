using GridTrack.Infrastructure.H3Service;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.UnitTests.H3Service;

public class H3GridServiceTests
{
    private static readonly GeometryFactory Factory = new();

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

        var cells = await service.GenerateGridBoundsAsync(33.50m, 33.52m, 36.27m, 36.29m, 8);

        await Assert.That(cells.Any()).IsTrue();
    }
}
