using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class ForecastReadService : IForecastReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public ForecastReadService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<ForecastDto?> GetForecastAsync(string districtId, DateTime forecastWindow, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               @DistrictId as "DistrictId",
                               @ForecastWindow as "ForecastWindow",
                               COUNT(*) as "ExpectedDeliveries",
                               NOW() as "GeneratedAt"
                           FROM public."Deliveries"
                           WHERE "DistrictId" = @DistrictId
                           AND "CreatedAt" >= @ForecastWindow
                           """;

        return await connection.QueryFirstOrDefaultAsync<ForecastDto>(sql, new { DistrictId = districtId, ForecastWindow = forecastWindow });
    }
}