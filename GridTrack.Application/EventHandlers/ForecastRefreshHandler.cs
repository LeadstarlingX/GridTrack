using GridTrack.Application.UseCases.Forecast;

namespace GridTrack.Application.EventHandlers;

public sealed class ForecastRefreshHandler
{
    public Task Handle(DistrictDemandForecastUpdated message, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
