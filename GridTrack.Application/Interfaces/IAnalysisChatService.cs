namespace GridTrack.Application.Interfaces;

/// <summary>
/// Sends a user question + CSV context to the Python AI service and returns the answer.
/// Returns null when the upstream service is unavailable.
/// </summary>
public interface IAnalysisChatService
{
    Task<string?> AskAsync(string question, string csvContext, CancellationToken ct);
}
