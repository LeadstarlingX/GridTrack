namespace GridTrack.Application.Interfaces;

/// <summary>Proxies a chat question + CSV context to the Python AI service; null when unavailable.</summary>
public interface IAnalysisChatService
{
    Task<string?> AskAsync(string question, string csvContext, CancellationToken ct);

    /// <summary>Streams token chunks from Python SSE. Yields empty on failure.</summary>
    IAsyncEnumerable<string> StreamAsync(string question, string csvContext, CancellationToken ct);

    /// <summary>Transcribes raw audio via Groq Whisper. Returns null when unavailable.</summary>
    Task<string?> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct);
}
