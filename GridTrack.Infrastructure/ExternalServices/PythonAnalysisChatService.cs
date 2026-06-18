using System.IO;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
            return null;
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string question,
        string csvContext,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Open the SSE stream in a helper to keep yield outside try/catch (C# requirement).
        var reader = await TryOpenStreamReaderAsync(question, csvContext, ct);
        if (reader is null) yield break;

        using (reader)
        {
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                string? line;
                try { line = await reader.ReadLineAsync(ct); }
                catch { yield break; }

                if (line is null || !line.StartsWith("data: ")) continue;

                var data = line["data: ".Length..];
                if (data == "[DONE]") yield break;

                string token;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    token = doc.RootElement.GetProperty("token").GetString() ?? "";
                }
                catch { continue; }

                if (!string.IsNullOrEmpty(token))
                    yield return token;
            }
        }
    }

    private async Task<StreamReader?> TryOpenStreamReaderAsync(
        string question, string csvContext, CancellationToken ct)
    {
        try
        {
            var body = JsonSerializer.Serialize(new
            {
                question,
                context = new { csv = csvContext },
            });
            var request = new HttpRequestMessage(HttpMethod.Post, "/chat/stream")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return new StreamReader(stream);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(audio);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "file", fileName);

            var response = await http.PostAsync("/transcribe", form, ct);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<PythonTranscribeResponse>(
                cancellationToken: ct);
            return result?.Text;
        }
        catch
        {
            return null;
        }
    }

    private sealed record PythonChatResponse(string Answer);
    private sealed record PythonTranscribeResponse(string Text);
}
