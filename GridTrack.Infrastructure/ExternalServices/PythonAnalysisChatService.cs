using System.Net.Http.Json;
using GridTrack.Application.Interfaces;

namespace GridTrack.Infrastructure.ExternalServices;

internal sealed class PythonAnalysisChatService(HttpClient http) : IAnalysisChatService
{
    public async Task<string?> AskAsync(string question, string csvContext, CancellationToken ct)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/chat", new
            {
                question,
                context = new { csv = csvContext },
            }, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<PythonChatResponse>(
                cancellationToken: ct);

            return result?.Answer;
        }
        catch
        {
            // Service unreachable or timed out — caller maps null → 503
            return null;
        }
    }

    private sealed record PythonChatResponse(string Answer);
}
