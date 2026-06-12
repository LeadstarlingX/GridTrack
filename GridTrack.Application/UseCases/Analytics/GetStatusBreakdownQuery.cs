using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analytics;

public sealed record GetStatusBreakdownQuery(DateTime? From = null, DateTime? To = null);

public sealed class GetStatusBreakdownHandler
{
    private static readonly string[] StatusLabels =
        ["Created", "Assigned", "PickedUp", "InTransit", "Delivered", "Cancelled", "Anomalous"];

    public async Task<GetStatusBreakdownResponse> Handle(
        GetStatusBreakdownQuery query,
        ISqlConnectionFactory sqlConnectionFactory,
        CancellationToken ct)
    {
        var from = query.From ?? DateTime.UtcNow.Date;
        var to   = (query.To ?? DateTime.UtcNow.Date).Date.AddDays(1);

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
