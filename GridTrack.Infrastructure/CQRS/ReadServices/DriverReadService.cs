using Dapper;
using GridTrack.Application.Abstractions.Data;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;
using NetTopologySuite.Geometries;

namespace GridTrack.Infrastructure.CQRS.ReadServices;

public sealed class DriverReadService : IDriverReadService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public DriverReadService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<IEnumerable<DriverDto>> GetByDistrictAsync(string districtId, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DriverId",
                               "Location",
                               "IsActive",
                               "LastSeen",
                               "DistrictId"
                           FROM public."Drivers"
                           WHERE "DistrictId" = @DistrictId
                           ORDER BY "LastSeen" DESC
                           """;

        return await connection.QueryAsync<DriverDto>(sql, new { DistrictId = districtId });
    }

    public async Task<IEnumerable<DriverDto>> GetNearestAsync(Point location, int count, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               "DriverId",
                               "Location",
                               "IsActive",
                               "LastSeen",
                               "DistrictId"
                           FROM public."Drivers"
                           WHERE "IsActive" = true
                           ORDER BY "Location" <-> ST_GeogFromText(@LocationWkt)
                           LIMIT @Count
                           """;

        var locationWkt = $"POINT({location.X} {location.Y})";
        return await connection.QueryAsync<DriverDto>(sql, new { LocationWkt = locationWkt, Count = count });
    }
}