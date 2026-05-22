using System.Text;
using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class ExportReadService : IExportReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public ExportReadService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<ExportCsvResult> ExportDeliveriesAsync(
        string mode,
        DateTime? from,
        DateTime? to,
        IReadOnlyList<string>? days,
        int? fromHour,
        int? toHour,
        CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        // Days filter maps day-of-week names to PostgreSQL dow (0=Sunday … 6=Saturday)
        int[]? dowFilter = null;
        if (days is { Count: > 0 })
        {
            dowFilter = days
                .Select(d => d.ToLower() switch
                {
                    "sunday"    => 0,
                    "monday"    => 1,
                    "tuesday"   => 2,
                    "wednesday" => 3,
                    "thursday"  => 4,
                    "friday"    => 5,
                    "saturday"  => 6,
                    _           => -1,
                })
                .Where(v => v >= 0)
                .ToArray();
        }

        // Build WHERE clauses dynamically to avoid untyped NULL parameters in Npgsql
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (from.HasValue)
        {
            where.Add("\"CreatedAt\" >= @From");
            parameters.Add("From", from.Value);
        }
        if (to.HasValue)
        {
            where.Add("\"CreatedAt\" <= @To");
            parameters.Add("To", to.Value);
        }
        if (fromHour.HasValue)
        {
            where.Add("""EXTRACT(HOUR FROM "CreatedAt" AT TIME ZONE 'UTC') >= @FromHour""");
            parameters.Add("FromHour", fromHour.Value);
        }
        if (toHour.HasValue)
        {
            where.Add("""EXTRACT(HOUR FROM "CreatedAt" AT TIME ZONE 'UTC') <= @ToHour""");
            parameters.Add("ToHour", toHour.Value);
        }
        if (dowFilter is { Length: > 0 })
        {
            where.Add("""EXTRACT(DOW FROM "CreatedAt" AT TIME ZONE 'UTC')::int = ANY(@DowFilter)""");
            parameters.Add("DowFilter", dowFilter);
        }

        var whereClause = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;

        var sql = $"""
                   SELECT
                       "DeliveryId",
                       "Status",
                       "DistrictId",
                       "AssignedDriverId",
                       "CreatedAt",
                       "PickedUpAt",
                       "DeliveredAt",
                       "CancelledAt",
                       "AnomalyFlag",
                       "AnomalyReason"
                   FROM public."Deliveries"
                   {whereClause}
                   ORDER BY "CreatedAt" DESC
                   """;

        var rows = await connection.QueryAsync<DeliveryExportRow>(sql, parameters);

        var stream = BuildCsvStream(rows);
        var fileName = $"deliveries_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return new ExportCsvResult(stream, fileName);
    }

    private static Stream BuildCsvStream(IEnumerable<DeliveryExportRow> rows)
    {
        var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);

        // Header
        writer.WriteLine(
            "DeliveryId,Status,DistrictId,AssignedDriverId," +
            "CreatedAt,PickedUpAt,DeliveredAt,CancelledAt," +
            "AnomalyFlag,AnomalyReason");

        foreach (var r in rows)
        {
            writer.WriteLine(string.Join(',',
                r.DeliveryId,
                r.Status,
                Escape(r.DistrictId),
                r.AssignedDriverId?.ToString() ?? string.Empty,
                r.CreatedAt.ToString("O"),
                r.PickedUpAt?.ToString("O") ?? string.Empty,
                r.DeliveredAt?.ToString("O") ?? string.Empty,
                r.CancelledAt?.ToString("O") ?? string.Empty,
                r.AnomalyFlag,
                Escape(r.AnomalyReason ?? string.Empty)));
        }

        writer.Flush();
        ms.Position = 0;
        return ms;
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // ── Private Dapper DTO ─────────────────────────────────────────────────
    private sealed record DeliveryExportRow(
        Guid DeliveryId,
        int Status,
        string DistrictId,
        Guid? AssignedDriverId,
        DateTime CreatedAt,
        DateTime? PickedUpAt,
        DateTime? DeliveredAt,
        DateTime? CancelledAt,
        bool AnomalyFlag,
        string? AnomalyReason);
}
