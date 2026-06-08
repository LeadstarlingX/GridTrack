using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;

namespace GridTrack.Application.UseCases.Analysis;

public sealed record AnalysisChatQuery(IEnumerable<ChatMessageDto> Messages, string CsvData);

public sealed class AnalysisChatHandler
{
    public async Task<ChatResponse?> Handle(
        AnalysisChatQuery query,
        IAnalysisChatService chatService,
        CancellationToken ct)
    {
        // Extract the last user turn as the question
        var question = query.Messages
            .LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            ?.Content
            ?? query.Messages.LastOrDefault()?.Content;

        if (string.IsNullOrWhiteSpace(question))
            return new ChatResponse("Please provide a question.");

        var answer = await chatService.AskAsync(question, query.CsvData, ct);

        // null means the Python service is unavailable — controller maps this to 503
        return answer is null ? null : new ChatResponse(answer);
    }
}
