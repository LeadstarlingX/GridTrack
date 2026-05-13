using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IForecastingService
{
    Task<ForecastDto?> GetDistrictDemandForecastAsync(string districtId, DateTime forecastWindow);
    Task<IEnumerable<AnomalyAlertDto>> GetEtaAnomaliesAsync(IEnumerable<string> districtIds);
}
