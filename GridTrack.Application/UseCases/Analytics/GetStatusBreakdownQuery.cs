using Dapper;
using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetStatusBreakdownQuery(DateTime? From = null, DateTime? To = null);

public sealed class GetStatusBreakdownHandler
{
    private static readonly string[] StatusLabels =
        ["Created", "Assigned", "PickedUp", "InTransit", "Delivered", "Cancelled", "Anomalous"];

    public Task<GetStatusBreakdownResponse> Handle(
        GetStatusBreakdownQuery query,
        ISqlConnectionFactory sqlConnectionFactory,
        ICacheService cache,
        CancellationToken ct)
    {
        var from = query.From ?? DateTime.UtcNow.Date;
        var to   = (query.To ?? DateTime.UtcNow.Date).Date.AddDays(1);

        var today = DateTime.UtcNow.Date;
        var ttl = (query.To ?? today).Date >= today
            ? TimeSpan.FromSeconds(45)
            : TimeSpan.FromMinutes(5);

        var key = $"analytics:status-bd:{from:yyyyMMdd}:{to:yyyyMMdd}";
        return cache.GetOrSetAsync(key, innerCt => QueryAsync(from, to, sqlConnectionFactory, innerCt), ttl, ct);
    }

    private async Task<GetStatusBreakdownResponse> QueryAsync(
        DateTime from, DateTime to,
        ISqlConnectionFactory sqlConnectionFactory,
        CancellationToken ct)
    {
        using var connection = sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT "Status", COUNT(*)::int AS "Count"
            FROM public."Deliveries"
            WHERE "CreatedAt" >= @From AND "CreatedAt" < @To
            GROUP BY "Status"
            ORDER BY "Status"
            """;

        var rows = await connection.QueryAsync<(int Status, int Count)>(
            sql, new { From = from, To = to });

        var items = rows
            .Select(r => new StatusBreakdownItemResponse(
                r.Status,
                r.Status < StatusLabels.Length ? StatusLabels[r.Status] : $"Status{r.Status}",
                r.Count))
            .ToList();

        return new GetStatusBreakdownResponse(items);
    }
}
