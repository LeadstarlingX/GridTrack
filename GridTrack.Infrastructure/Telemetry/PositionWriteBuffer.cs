using System.Collections.Concurrent;
using GridTrack.Application.Abstractions.Telemetry;

namespace GridTrack.Infrastructure.Telemetry;

internal sealed class PositionWriteBuffer : IPositionWriteBuffer
{
    // ConcurrentDictionary provides lock-free last-write-wins semantics per driver key.
    // Interlocked.Exchange on Drain atomically swaps the reference so no lock is held
    // during the ClickHouse/Postgres flush.
    private ConcurrentDictionary<Guid, PositionRecord> _buffer = new();

    public void Write(Guid driverId, double lat, double lng, string districtId, DateTime recordedAt)
        => _buffer[driverId] = new PositionRecord(driverId, lat, lng, districtId, recordedAt);

    public IReadOnlyList<PositionRecord> Drain()
    {
        var snapshot = Interlocked.Exchange(ref _buffer, new ConcurrentDictionary<Guid, PositionRecord>());
        return snapshot.Values.ToArray();
    }
}
