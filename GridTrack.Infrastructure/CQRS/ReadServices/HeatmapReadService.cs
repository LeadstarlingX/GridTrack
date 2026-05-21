using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class HeatmapReadService : IHeatmapReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public HeatmapReadService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<IEnumerable<HeatmapPointDto>> GetHeatmapAsync(string districtId, DateTime window, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               ST_Y("CurrentLocation"::geometry) as "Latitude",
                               ST_X("CurrentLocation"::geometry) as "Longitude",
                               CASE
                                   WHEN "Status" = 0 THEN 1.0
                                   WHEN "Status" = 1 THEN 0.8
                                   WHEN "Status" = 2 THEN 0.6
                                   ELSE 0.4
                               END as "Intensity"
                           FROM public."Deliveries"
                           WHERE "DistrictId" = @DistrictId
                           AND "CreatedAt" >= @Window
                           """;

        return await connection.QueryAsync<HeatmapPointDto>(sql, new { DistrictId = districtId, Window = window });
    }
}