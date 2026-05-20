namespace GridTrack.Presentation.Controllers.Analysis;

public sealed class ChatRequest
{
    public required IEnumerable<ChatMessage> Messages { get; init; }
    public required string CsvData { get; init; }
}

public sealed class ChatMessage
{
    public required string Role { get; init; } // "user" | "assistant"
    public required string Content { get; init; }
}