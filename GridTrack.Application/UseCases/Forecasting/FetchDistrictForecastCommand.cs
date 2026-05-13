using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Forecasting;

public sealed record ForecastRequest(string DistrictId, DateTime ForecastWindow);

public sealed record FetchDistrictForecastCommand(ForecastRequest Request);

public sealed class FetchDistrictForecastHandler
{
    public async Task<Result<ForecastDto>> Handle(
        FetchDistrictForecastCommand command,
        IForecastingService forecastingService,
        CancellationToken ct)
    {
        var request = command.Request;
        var forecast = await forecastingService.GetDistrictDemandForecastAsync(
            request.DistrictId,
            request.ForecastWindow);

        if (forecast is null)
        {
            return Result.Failure<ForecastDto>(ApplicationErrors.ForecastNotFound);
        }

        return Result.Success(forecast);
    }
}
