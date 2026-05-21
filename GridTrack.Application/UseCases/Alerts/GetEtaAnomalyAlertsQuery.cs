using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Alerts;

public sealed record AnomalyFilterRequest(IEnumerable<string> DistrictIds);

public sealed record GetEtaAnomalyAlertsQuery(AnomalyFilterRequest Request);

public sealed class GetEtaAnomalyAlertsHandler
{
    public async Task<Result<IEnumerable<AnomalyAlertDto>>> Handle(
        GetEtaAnomalyAlertsQuery query,
        IAnomalyReadService readService,
        CancellationToken ct)
    {
        var anomalies = await readService.GetEtaAnomaliesAsync(query.Request.DistrictIds, ct);
        return Result.Success(anomalies);
    }
}
