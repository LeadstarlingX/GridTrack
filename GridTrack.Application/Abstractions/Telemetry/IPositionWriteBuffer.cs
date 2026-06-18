namespace GridTrack.Application.Abstractions.Telemetry;

public sealed record PositionRecord(
    Guid DriverId,
    double Lat,
    double Lng,
    string DistrictId,
    DateTime RecordedAt);

public sealed record CachedDriverMetadata(
    string Name,
    string ShortName,
    string DistrictId,
    bool IsActive);

public interface IPositionWriteBuffer
{
    // Last-write-wins per driver: write is always the latest position.
    void Write(Guid driverId, double lat, double lng, string districtId, DateTime recordedAt);

    // Atomically swap the buffer out and return a snapshot for flushing.
    IReadOnlyList<PositionRecord> Drain();
}
