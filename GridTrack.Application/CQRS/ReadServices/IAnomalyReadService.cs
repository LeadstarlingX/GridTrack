using GridTrack.Application.Dtos;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IAnomalyReadService
{
    Task<IEnumerable<AnomalyAlertDto>> GetEtaAnomaliesAsync(IEnumerable<string> districtIds, CancellationToken ct);

    Task<GetAlertsResponse> GetPaginatedAlertsAsync(
        string? cursor,
        DateTime? from,
        DateTime? to,
        string? districtId,
        string? anomalyType,
        int pageSize,
        CancellationToken ct);
}
