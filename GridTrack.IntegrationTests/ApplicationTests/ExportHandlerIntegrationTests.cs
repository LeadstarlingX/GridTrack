using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Export;
using GridTrack.Domain.Deliveries;
using GridTrack.IntegrationTests.Abstractions;
using NetTopologySuite.Geometries;
using System.Text;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class ExportHandlerIntegrationTests : BaseIntegrationTest
{
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);
    private static Point Damascus => GeoFactory.CreatePoint(new Coordinate(36.2765, 33.5138));

    private static Delivery CreateDelivery(string districtId = "h3-export", DateTime? createdAt = null)
    {
        var r = Delivery.Create(Guid.NewGuid(), Damascus, districtId, createdAt ?? DateTime.UtcNow, null);
        r.IsSuccess.Should().BeTrue();
        r.Value.ClearDomainEvents();
        return r.Value;
    }

    [Test]
    [NotInParallel(Order = 900)]
    public async Task ExportCsvCommand_Should_Return_Empty_Csv_When_No_Deliveries()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<ExportCsvResult>(
            new ExportCsvCommand("deliveries", null, null, null, null, null));

        result.Should().NotBeNull();
        result.FileName.Should().EndWith(".csv");
        result.CsvStream.Should().NotBeNull();

        var reader = new StreamReader(result.CsvStream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();

        // Should have header row only
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1); // only header
        lines[0].Should().Contain("DeliveryId");
    }

    [Test]
    [NotInParallel(Order = 901)]
    public async Task ExportCsvCommand_Should_Include_All_Deliveries_In_Csv()
    {
        await ResetDatabaseAsync();

        await SeedDeliveriesAsync([CreateDelivery(), CreateDelivery(), CreateDelivery()]);

        var result = await InvokeAsync<ExportCsvResult>(
            new ExportCsvCommand("deliveries", null, null, null, null, null));

        var reader = new StreamReader(result.CsvStream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(4); // header + 3 data rows
    }

    [Test]
    [NotInParallel(Order = 902)]
    public async Task ExportCsvCommand_Should_Filter_By_DateRange()
    {
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        await SeedDeliveriesAsync([
            CreateDelivery(createdAt: now.AddDays(-10)), // outside range
            CreateDelivery(createdAt: now.AddDays(-1)),  // inside range
            CreateDelivery(createdAt: now),              // inside range
        ]);

        var result = await InvokeAsync<ExportCsvResult>(
            new ExportCsvCommand("deliveries", now.AddDays(-2), now.AddDays(1), null, null, null));

        var reader = new StreamReader(result.CsvStream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(3); // header + 2 data rows
    }
}
