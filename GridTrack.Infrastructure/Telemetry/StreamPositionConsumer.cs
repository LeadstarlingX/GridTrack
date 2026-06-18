using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using StackExchange.Redis;
using System.Globalization;

namespace GridTrack.Infrastructure.Telemetry;

internal sealed class StreamPositionConsumer(
    IConnectionMultiplexer redis,
    IDashboardPushService push,
    ILogger<StreamPositionConsumer> logger) : BackgroundService
{
    private const string StreamKey    = "telemetry:positions";
    private const string GroupName    = "hub-consumers";
    private const string ConsumerName = "hub-0";
    private const int    BatchSize    = 200;
    private const int    IdleDelayMs  = 5;

    private static readonly GeometryFactory GeoFactory = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await EnsureConsumerGroupAsync(db);

        while (!ct.IsCancellationRequested)
        {
            StreamEntry[] entries;
            try
            {
                entries = await db.StreamReadGroupAsync(
                    StreamKey, GroupName, ConsumerName,
                    position: ">", count: BatchSize, noAck: true);
            }
            catch (RedisServerException ex) when (!ct.IsCancellationRequested &&
                (ex.Message.Contains("NOGROUP",    StringComparison.OrdinalIgnoreCase) ||
                 ex.Message.Contains("no such key", StringComparison.OrdinalIgnoreCase)))
            {
                // Stream or consumer group was deleted (e.g. FLUSHDB in integration tests).
                // Recreate from the beginning so we pick up any entries that were XADD'd
                // before the group came back.
                await EnsureConsumerGroupAsync(db, fromBeginning: true);
                await Task.Delay(50, ct);
                continue;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Stream read failed — retrying in 1 s");
                await Task.Delay(1_000, ct);
                continue;
            }

            if (entries.Length == 0)
            {
                await Task.Delay(IdleDelayMs, ct);
                continue;
            }

            foreach (var entry in entries)
            {
                try { await ProcessEntryAsync(entry, push, ct); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to process stream entry {Id}", entry.Id);
                }
            }
        }
    }

    private async Task EnsureConsumerGroupAsync(IDatabase db, bool fromBeginning = false)
    {
        var position = fromBeginning ? StreamPosition.Beginning : StreamPosition.NewMessages;
        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, position, createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists — normal on restart.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not create consumer group — reads will fail until Redis is reachable");
        }
    }

    private static string Field(StreamEntry e, string key) => (string?)e[key] ?? string.Empty;

    private static async Task ProcessEntryAsync(StreamEntry entry, IDashboardPushService push, CancellationToken ct)
    {
        var driverId   = Guid.Parse(Field(entry, "driverId"));
        var lat        = double.Parse(Field(entry, "lat"),        CultureInfo.InvariantCulture);
        var lng        = double.Parse(Field(entry, "lng"),        CultureInfo.InvariantCulture);
        var districtId = Field(entry, "districtId");
        var name       = Field(entry, "name");
        var shortName  = Field(entry, "shortName");
        var isActive   = Field(entry, "isActive") == "1";
        var ts         = DateTime.Parse(Field(entry, "ts"), null, DateTimeStyles.RoundtripKind);

        await BroadcastAsync(driverId, lat, lng, districtId, name, shortName, isActive, ts, push, ct);
    }

    // Internal so Infrastructure.UnitTests can test the mapping + push call without
    // needing to construct a StreamEntry (whose constructor is internal to SE.Redis).
    internal static async Task BroadcastAsync(
        Guid driverId, double lat, double lng,
        string districtId, string name, string shortName, bool isActive, DateTime ts,
        IDashboardPushService push, CancellationToken ct)
    {
        var dto = new DriverDto
        {
            DriverId   = driverId,
            Location   = GeoFactory.CreatePoint(new Coordinate(lng, lat)),
            IsActive   = isActive,
            LastSeen   = ts,
            DistrictId = districtId,
            Name       = name,
            ShortName  = shortName,
        };
        await push.BroadcastDriverPositionAsync(districtId, dto, ct);
    }
}
