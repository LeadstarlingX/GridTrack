using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.UseCases.Integration;

public sealed record TelemetryBatchRequest(IEnumerable<TelemetryItemDto> Items);

public sealed record ProcessExternalTelemetryCommand(TelemetryBatchRequest Request);

public sealed class ProcessExternalTelemetryHandler
{
    public async Task<Result<BatchIngestResult>> Handle(
        ProcessExternalTelemetryCommand command,
        IEventPublisher eventPublisher,
        CancellationToken ct)
    {
        var items = command.Request.Items?.ToList() ?? new List<TelemetryItemDto>();
        if (items.Count == 0)
        {
            return Result.Failure<BatchIngestResult>(ApplicationErrors.InvalidTelemetry);
        }

        var accepted = items.Count;
        var rejected = 0;

        var processedEvent = new ExternalTelemetryProcessed(Guid.NewGuid(), accepted, rejected, DateTime.UtcNow);
        await eventPublisher.PublishAsync(processedEvent, ct);

        return Result.Success(new BatchIngestResult(accepted, rejected));
    }
}

public sealed record ExternalTelemetryProcessed(
    Guid BatchId,
    int Accepted,
    int Rejected,
    DateTime ProcessedAt) : IDomainEvent;
