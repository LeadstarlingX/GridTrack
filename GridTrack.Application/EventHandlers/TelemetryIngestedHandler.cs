using GridTrack.Application.UseCases.Integration;

namespace GridTrack.Application.EventHandlers;

public sealed class TelemetryIngestedHandler
{
    public Task Handle(ExternalTelemetryProcessed message, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
