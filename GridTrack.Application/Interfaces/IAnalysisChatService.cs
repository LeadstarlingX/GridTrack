namespace GridTrack.Application.Interfaces;

/// <summary>Proxies a chat question + CSV context to the Python AI service; null when unavailable.</summary>
public interface IAnalysisChatService
{
    Task<string?> AskAsync(string question, string csvContext, CancellationToken ct);
}
