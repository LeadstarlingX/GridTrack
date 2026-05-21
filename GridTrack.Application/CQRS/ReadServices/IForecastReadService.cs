using GridTrack.Application.Dtos;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IForecastReadService
{
    Task<ForecastDto?> GetForecastAsync(string districtId, DateTime forecastWindow, CancellationToken ct);
}
