using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IForecastReadService
{
    Task<ForecastDto?> GetForecastAsync(string districtId, DateTime forecastWindow, CancellationToken ct);
}
