using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Analysis;

public sealed record AnalysisChatQuery(IEnumerable<ChatMessageDto> Messages, string CsvData);

public sealed class AnalysisChatHandler
{
    public Task<ChatResponse> Handle(AnalysisChatQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}
