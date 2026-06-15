using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;

namespace GridTrack.Infrastructure.ExternalServices;

internal sealed class PythonForecastService(HttpClient http) : IForecastService
{
    public async Task<StaffingForecastResponse?> GetStaffingAsync(
        string   districtId,
        DateTime targetAt,
        double   historicalAvgDeliveries,
        bool     recentSurgeDetected,
        CancellationToken ct)
    {
        try
        {
            var body = new
            {
                district                   = districtId,
                target_datetime            = targetAt.ToString("O"),
                day_of_week                = (int)targetAt.DayOfWeek == 0 ? 6 : (int)targetAt.DayOfWeek - 1,
                target_hour                = targetAt.Hour,
                historical_avg_deliveries  = historicalAvgDeliveries,
                recent_surge_detected      = recentSurgeDetected,
            };

            var response = await http.PostAsJsonAsync("/staffing", body, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<PythonStaffingResponse>(
                cancellationToken: ct);

            return result is null ? null : new StaffingForecastResponse(
                districtId,
                targetAt,
                result.RecommendedDrivers,
                result.Confidence,
                result.Reasoning,
                historicalAvgDeliveries);
        }
        catch
        {
            return null;
        }
    }

    private sealed record PythonStaffingResponse(
        [property: JsonPropertyName("recommended_drivers")] int    RecommendedDrivers,
        [property: JsonPropertyName("confidence")]          string Confidence,
        [property: JsonPropertyName("reasoning")]           string Reasoning);
}
