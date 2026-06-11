using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Districts;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Districts;

[ApiController]
[Route("/api/districts")]
public class DistrictsController(IMessageBus bus, ISqlConnectionFactory sql) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDistricts(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDistrictsResponse>(
            new GetDistrictsQuery(),
            ct);

        return Ok(result.Items);
    }

    [HttpGet("boundaries")]
    public async Task<IActionResult> GetDistrictBoundaries(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDistrictBoundariesResponse>(
            new GetDistrictBoundariesQuery(),
            ct);

        return Ok(result);
    }

    [HttpGet("{districtId}/sparkline")]
    public async Task<IActionResult> GetSparkline(string districtId, [FromQuery] int hours = 6, CancellationToken ct = default)
    {
        using var conn = sql.CreateConnection();
        const string query = """
            SELECT
                date_trunc('hour', "CreatedAt") AS hour,
                COUNT(*)::int                   AS count
            FROM public."Deliveries"
            WHERE "DistrictId" = @DistrictId
              AND "CreatedAt"  >= NOW() - (@Hours * INTERVAL '1 hour')
            GROUP BY 1
            ORDER BY 1
            """;
        var rows = await conn.QueryAsync<SparklinePoint>(query, new { DistrictId = districtId, Hours = hours });
        return Ok(rows);
    }
}

file sealed record SparklinePoint(DateTime Hour, int Count);
