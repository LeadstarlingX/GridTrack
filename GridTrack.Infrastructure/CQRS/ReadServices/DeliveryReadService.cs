using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class DeliveryReadService : IDeliveryReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public DeliveryReadService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DeliveryId",
                               "CurrentLocation",
                               "Status",
                               "AssignedDriverId",
                               "ExpectedEta",
                               "ActualEta",
                               "DistrictId",
                               "AnomalyFlag",
                               "CreatedAt",
                               "PickedUpAt",
                               "DeliveredAt",
                               "CancelledAt",
                               "AnomalyReason"
                           FROM public."Deliveries"
                           WHERE "DeliveryId" = @Id
                           """;

        return await connection.QueryFirstOrDefaultAsync<DeliveryDto>(sql, new { Id = id });
    }

    public async Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DeliveryId",
                               "CurrentLocation",
                               "Status",
                               "AssignedDriverId",
                               "ExpectedEta",
                               "ActualEta",
                               "DistrictId",
                               "AnomalyFlag",
                               "CreatedAt",
                               "PickedUpAt",
                               "DeliveredAt",
                               "CancelledAt",
                               "AnomalyReason"
                           FROM public."Deliveries"
                           WHERE "DistrictId" = @DistrictId
                           ORDER BY "CreatedAt" DESC
                           """;

        return await connection.QueryAsync<DeliveryDto>(sql, new { DistrictId = districtId });
    }
}