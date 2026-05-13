using GridTrack.Application.UseCases.Forecasting;

namespace GridTrack.Application.EventHandlers;

public sealed class ForecastRefreshHandler
{
    public Task Handle(DistrictDemandForecastUpdated message, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
