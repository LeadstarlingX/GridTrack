using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class AnomalyReadService : IAnomalyReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public AnomalyReadService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<IEnumerable<AnomalyAlertDto>> GetEtaAnomaliesAsync(IEnumerable<string> districtIds, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DeliveryId",
                               "DistrictId",
                               0 as "Type",
                               "AnomalyReason",
                               "CreatedAt" as "Timestamp"
                           FROM public."Deliveries"
                           WHERE "AnomalyFlag" = true
                           AND "DistrictId" = ANY(@DistrictIds)
                           ORDER BY "CreatedAt" DESC
                           """;

        return await connection.QueryAsync<AnomalyAlertDto>(sql, new { DistrictIds = districtIds.ToArray() });
    }
}