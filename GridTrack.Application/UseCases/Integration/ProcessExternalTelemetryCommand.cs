using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Domain.Abstractions;
using System.Linq;

namespace GridTrack.Application.UseCases.Integration;

public sealed record TelemetryBatchRequest(IEnumerable<TelemetryItemDto> Items);

public sealed record ProcessExternalTelemetryCommand(TelemetryBatchRequest Request);

public sealed class ProcessExternalTelemetryHandler
{
    public async Task<(Result<BatchIngestResult> Result, IEnumerable<object> Events)> Handle(
        ProcessExternalTelemetryCommand command,
        CancellationToken ct)
    {
        var items = command.Request.Items?.ToList() ?? new List<TelemetryItemDto>();
        if (items.Count == 0)
        {
            return (Result.Failure<BatchIngestResult>(ApplicationErrors.InvalidTelemetry), Array.Empty<object>());
        }

        var accepted = items.Count;
        var rejected = 0;

        var processedEvent = new ExternalTelemetryProcessed(Guid.NewGuid(), accepted, rejected, DateTime.UtcNow);

        return (Result.Success(new BatchIngestResult(accepted, rejected)), new object[] { processedEvent });
    }
}

public sealed record ExternalTelemetryProcessed(
    Guid BatchId,
    int Accepted,
    int Rejected,
    DateTime ProcessedAt) : IDomainEvent;
