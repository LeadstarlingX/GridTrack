using System.Globalization;
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

    public async Task<GetAlertsResponse> GetPaginatedAlertsAsync(
        string? cursor,
        DateTime? from,
        DateTime? to,
        string? districtId,
        string? anomalyType,
        int pageSize,
        CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();

        // Decode cursor: ISO-8601 timestamp of the last item in the previous page
        DateTime? cursorTime = null;
        if (!string.IsNullOrWhiteSpace(cursor) &&
            DateTime.TryParse(cursor, null, DateTimeStyles.RoundtripKind, out var parsed))
        {
            cursorTime = parsed;
        }

        var fetchSize = pageSize > 0 ? pageSize + 1 : 1;

        // Build WHERE clauses dynamically to avoid untyped NULL parameters in Npgsql
        var where = new List<string> { """d."AnomalyFlag" = true""" };
        var parameters = new DynamicParameters();

        if (cursorTime.HasValue)
        {
            where.Add("""d."CreatedAt" < @CursorTime""");
            parameters.Add("CursorTime", cursorTime.Value);
        }
        if (from.HasValue)
        {
            where.Add("""d."CreatedAt" >= @From""");
            parameters.Add("From", from.Value);
        }
        if (to.HasValue)
        {
            // Treat the "to" bound as inclusive end-of-day: a date-only value arrives
            // as midnight, so anomalies flagged later the same day must still match.
            where.Add("""d."CreatedAt" < @To""");
            parameters.Add("To", to.Value.Date.AddDays(1));
        }
        if (!string.IsNullOrWhiteSpace(districtId))
        {
            where.Add("""d."DistrictId" = @DistrictId""");
            parameters.Add("DistrictId", districtId);
        }
        if (!string.IsNullOrWhiteSpace(anomalyType))
        {
            var typeInt = anomalyType switch
            {
                "EtaExceeded"    => 0,
                "RouteDeviation" => 1,
                "StalePosition"  => 2,
                "UnexpectedStop" => 3,
                _ => -1,
            };
            if (typeInt >= 0)
            {
                where.Add("""d."AnomalyTypeValue" = @AnomalyTypeValue""");
                parameters.Add("AnomalyTypeValue", typeInt);
            }
        }

        parameters.Add("FetchSize", fetchSize);

        var sql = $"""
                   SELECT
                       d."DeliveryId"                                     AS "Id",
                       d."DeliveryId"                                     AS "DeliveryId",
                       d."AssignedDriverId"                               AS "DriverId",
                       COALESCE(dr."Name", '')                           AS "DriverName",
                       CASE d."AnomalyTypeValue"
                           WHEN 0 THEN 'EtaExceeded'
                           WHEN 1 THEN 'RouteDeviation'
                           WHEN 2 THEN 'StalePosition'
                           WHEN 3 THEN 'UnexpectedStop'
                           ELSE 'EtaExceeded'
                       END                                                AS "AnomalyType",
                       COALESCE(d."AnomalyReason", '')                   AS "Reason",
                       d."DistrictId"                                     AS "DistrictId",
                       d."DistrictId"                                     AS "DistrictName",
                       ST_Y(d."CurrentLocation"::geometry)::float         AS "Lat",
                       ST_X(d."CurrentLocation"::geometry)::float         AS "Lng",
                       d."CreatedAt"                                      AS "Timestamp"
                   FROM public."Deliveries" d
                   LEFT JOIN public."Drivers" dr ON dr."DriverId" = d."AssignedDriverId"
                   WHERE {string.Join(" AND ", where)}
                   ORDER BY d."CreatedAt" DESC
                   LIMIT @FetchSize
                   """;

        var rows = (await connection.QueryAsync<AnomalyAlertItemResponse>(sql, parameters)).ToList();

        string? nextCursor = null;
        if (pageSize > 0 && rows.Count > pageSize)
        {
            rows.RemoveAt(rows.Count - 1);
            nextCursor = rows.Last().Timestamp.ToString("O");
        }

        return new GetAlertsResponse(rows, nextCursor);
    }
}