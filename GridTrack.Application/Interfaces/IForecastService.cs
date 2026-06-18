using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IForecastService
{
    Task<StaffingForecastResponse?> GetStaffingAsync(
        string districtId,
        DateTime targetAt,
        double historicalAvgDeliveries,
        bool recentSurgeDetected,
        CancellationToken ct);
}
