using GridTrack.Infrastructure.Telemetry;

namespace GridTrack.Infrastructure.UnitTests.Telemetry;

public class PositionWriteBufferTests
{
    [Test]
    public async Task Write_Should_Store_Position_For_Driver()
    {
        var buffer   = new PositionWriteBuffer();
        var driverId = Guid.NewGuid();

        buffer.Write(driverId, 33.51, 36.27, "mezzeh", DateTime.UtcNow);
        var drained = buffer.Drain();

        await Assert.That(drained).Count().IsEqualTo(1);
        await Assert.That(drained[0].DriverId).IsEqualTo(driverId);
        await Assert.That(drained[0].Lat).IsEqualTo(33.51);
        await Assert.That(drained[0].Lng).IsEqualTo(36.27);
        await Assert.That(drained[0].DistrictId).IsEqualTo("mezzeh");
    }

    [Test]
    public async Task Write_Same_Driver_Twice_Should_Keep_Latest_Position()
    {
        var buffer   = new PositionWriteBuffer();
        var driverId = Guid.NewGuid();

        buffer.Write(driverId, 33.50, 36.25, "mezzeh", DateTime.UtcNow.AddSeconds(-5));
        buffer.Write(driverId, 33.51, 36.27, "mezzeh", DateTime.UtcNow);
        var drained = buffer.Drain();

        await Assert.That(drained).Count().IsEqualTo(1);
        await Assert.That(drained[0].Lat).IsEqualTo(33.51);
        await Assert.That(drained[0].Lng).IsEqualTo(36.27);
    }

    [Test]
    public async Task Drain_Should_Clear_Buffer_So_Second_Drain_Returns_Empty()
    {
        var buffer = new PositionWriteBuffer();
        buffer.Write(Guid.NewGuid(), 33.51, 36.27, "mezzeh", DateTime.UtcNow);

        buffer.Drain();
        var second = buffer.Drain();

        await Assert.That(second).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Write_For_Multiple_Drivers_Should_Appear_In_Single_Drain()
    {
        var buffer = new PositionWriteBuffer();
        var idA    = Guid.NewGuid();
        var idB    = Guid.NewGuid();

        buffer.Write(idA, 33.51, 36.27, "mezzeh", DateTime.UtcNow);
        buffer.Write(idB, 33.52, 36.28, "malki",  DateTime.UtcNow);

        var drained = buffer.Drain();

        await Assert.That(drained).Count().IsEqualTo(2);
        await Assert.That(drained.Any(r => r.DriverId == idA)).IsTrue();
        await Assert.That(drained.Any(r => r.DriverId == idB)).IsTrue();
    }

    [Test]
    public async Task Write_Before_Drain_Then_Write_Again_Should_Appear_In_Second_Drain()
    {
        var buffer   = new PositionWriteBuffer();
        var driverId = Guid.NewGuid();

        buffer.Write(driverId, 33.50, 36.25, "mezzeh", DateTime.UtcNow);
        buffer.Drain();

        buffer.Write(driverId, 33.55, 36.30, "malki", DateTime.UtcNow);
        var second = buffer.Drain();

        await Assert.That(second).Count().IsEqualTo(1);
        await Assert.That(second[0].Lat).IsEqualTo(33.55);
    }
}
