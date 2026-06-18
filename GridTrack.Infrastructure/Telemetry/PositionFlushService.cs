using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Abstractions.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Data;

namespace GridTrack.Infrastructure.Telemetry;

// Drains the in-memory write buffer every 5 s:
//   1. Batch-updates LastKnownLocation + LastSeen in Postgres (keeps dispatch queries fresh).
//   2. Bulk-inserts history rows into ClickHouse (cold path analytics).
//
// Both paths are optional/resilient: if either fails the error is logged and the
// next flush cycle retries. At 10K drivers × 1 Hz, each flush produces at most
// 10K deduplicated rows (last-write-wins per driver).
internal sealed class PositionFlushService(
    IPositionWriteBuffer buffer,
    ISqlConnectionFactory sqlFactory,
    IConfiguration configuration,
    ILogger<PositionFlushService> logger) : BackgroundService
{
    private const int FlushIntervalMs = 5_000;

    private readonly string? _clickHouseCs =
        configuration.GetConnectionString("ClickHouse");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_clickHouseCs))
            await EnsureClickHouseTableAsync(ct);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(FlushIntervalMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var records = buffer.Drain();
            if (records.Count == 0) continue;

            await FlushToPostgresAsync(records, ct);

            if (!string.IsNullOrWhiteSpace(_clickHouseCs))
                await FlushToClickHouseAsync(records, ct);
        }
    }

    // ── Postgres batch UPDATE ──────────────────────────────────────────────────
    // Single unnest() call: one round-trip for all N drivers.
    // Keeps LastKnownLocation fresh enough for PostGIS dispatch queries (±5 s).
    private async Task FlushToPostgresAsync(IReadOnlyList<PositionRecord> records, CancellationToken ct)
    {
        const string sql = """
            UPDATE "Drivers" d
            SET "Location" = ST_SetSRID(ST_MakePoint(u.lng, u.lat), 4326),
                "LastSeen"  = u.ts
            FROM unnest(
                @ids        ::uuid[],
                @lats       ::double precision[],
                @lngs       ::double precision[],
                @timestamps ::timestamptz[]
            ) AS u(id, lat, lng, ts)
            WHERE d."DriverId" = u.id
            """;

        try
        {
            await using var conn = (NpgsqlConnection)sqlFactory.CreateConnection();
            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                { Value = records.Select(r => r.DriverId).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("lats", NpgsqlDbType.Array | NpgsqlDbType.Double)
                { Value = records.Select(r => r.Lat).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("lngs", NpgsqlDbType.Array | NpgsqlDbType.Double)
                { Value = records.Select(r => r.Lng).ToArray() });
            cmd.Parameters.Add(new NpgsqlParameter("timestamps", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
                { Value = records.Select(r => r.RecordedAt).ToArray() });

            await cmd.ExecuteNonQueryAsync(ct);

            logger.LogDebug("Flushed {Count} positions to Postgres", records.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Postgres position flush failed for {Count} records", records.Count);
        }
    }

    // ── ClickHouse bulk insert ─────────────────────────────────────────────────
    // ClickHouseBulkCopy streams rows over HTTP — one request for the whole batch.
    // At 10K rows/flush this is ~50 KB, well within a single HTTP call.
    private async Task FlushToClickHouseAsync(IReadOnlyList<PositionRecord> records, CancellationToken ct)
    {
        try
        {
            await using var conn = new ClickHouseConnection(_clickHouseCs);
            await conn.OpenAsync(ct);

            var table = new DataTable();
            table.Columns.Add("driver_id",   typeof(Guid));
            table.Columns.Add("lat",         typeof(double));
            table.Columns.Add("lng",         typeof(double));
            table.Columns.Add("district_id", typeof(string));
            table.Columns.Add("recorded_at", typeof(DateTime));

            foreach (var r in records)
                table.Rows.Add(r.DriverId, r.Lat, r.Lng, r.DistrictId, r.RecordedAt);

            using var bulkCopy = new ClickHouseBulkCopy(conn)
            {
                DestinationTableName = "driver_positions",
                BatchSize = 100_000,
                ColumnNames = ["driver_id", "lat", "lng", "district_id", "recorded_at"],
            };
            await bulkCopy.InitAsync();
            await bulkCopy.WriteToServerAsync(table.CreateDataReader(), ct);

            logger.LogDebug("Flushed {Count} positions to ClickHouse", records.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ClickHouse position flush failed for {Count} records", records.Count);
        }
    }

    private async Task EnsureClickHouseTableAsync(CancellationToken ct)
    {
        const string ddl = """
            CREATE TABLE IF NOT EXISTS driver_positions (
                driver_id     UUID,
                lat           Float64,
                lng           Float64,
                district_id   String,
                recorded_at   DateTime64(3, 'UTC')
            )
            ENGINE = MergeTree()
            PARTITION BY toYYYYMMDD(recorded_at)
            ORDER BY (driver_id, recorded_at)
            TTL toDateTime(recorded_at) + INTERVAL 30 DAY DELETE
            SETTINGS index_granularity = 8192
            """;

        try
        {
            await using var conn = new ClickHouseConnection(_clickHouseCs);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ddl;
            await cmd.ExecuteNonQueryAsync(ct);
            logger.LogInformation("ClickHouse driver_positions table ready");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create ClickHouse driver_positions table — ClickHouse flush will be skipped");
        }
    }
}
