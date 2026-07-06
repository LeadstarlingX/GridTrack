using GridTrack.Application.Abstractions.Telemetry;
using StackExchange.Redis;

namespace GridTrack.Infrastructure.Telemetry;

internal sealed class RedisPositionStreamPublisher(IConnectionMultiplexer redis) : IPositionStreamPublisher
{
    private const string StreamKey = "telemetry:positions";

    public ValueTask PublishAsync(
        Guid driverId,
        double lat, double lng,
        string districtId,
        string name, string shortName, bool isActive,
        DateTime ts,
        CancellationToken ct)
    {
        var db = redis.GetDatabase();
        // FireAndForget keeps the hot path non-blocking — we don't need the stream entry ID.
        // Numerics use implicit RedisValue conversions (no ToString allocation for lat/lng/ts/isActive).
        db.StreamAddAsync(
            StreamKey,
            [
                new NameValueEntry("driverId",   driverId.ToString()),
                new NameValueEntry("lat",         lat),
                new NameValueEntry("lng",         lng),
                new NameValueEntry("districtId",  districtId),
                new NameValueEntry("name",        name),
                new NameValueEntry("shortName",   shortName),
                new NameValueEntry("isActive",    isActive ? 1 : 0),
                new NameValueEntry("ts",          new DateTimeOffset(ts.ToUniversalTime()).ToUnixTimeMilliseconds()),
            ],
            maxLength: 100_000,
            useApproximateMaxLength: true,
            flags: CommandFlags.FireAndForget);

        return ValueTask.CompletedTask;
    }
}
