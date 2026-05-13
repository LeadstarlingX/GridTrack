using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IAnomalyReadService
{
    Task<IEnumerable<AnomalyAlertDto>> GetEtaAnomaliesAsync(IEnumerable<string> districtIds, CancellationToken ct);
}
