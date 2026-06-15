using System.Text;
using System.Text.Json;
using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Application.UseCases.Analysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Analysis;

[ApiController]
[Route("/api/analysis")]
public class AnalysisController(IMessageBus bus, IAnalysisChatService chatService) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ChatResponse?>(
            new AnalysisChatQuery(request.Messages, request.CsvData),
            ct);

        return result is null
            ? StatusCode(503, new { code = "AI_UNAVAILABLE", message = "AI service is temporarily unavailable." })
            : Ok(result);
    }

    /// <summary>POST /api/analysis/chat/stream — streams SSE tokens for a chat question.</summary>
    [HttpPost("chat/stream")]
    public async Task StreamChat(
        [FromBody] StreamChatRequest request,
        CancellationToken ct = default)
    {
        Response.Headers["Content-Type"]      = "text/event-stream";
        Response.Headers["Cache-Control"]     = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var token in chatService.StreamAsync(request.Question, request.CsvData, ct))
        {
            var data  = JsonSerializer.Serialize(new { token });
            var bytes = Encoding.UTF8.GetBytes($"data: {data}\n\n");
            await Response.Body.WriteAsync(bytes, ct);
            await Response.Body.FlushAsync(ct);
        }

        var done = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
        await Response.Body.WriteAsync(done, ct);
        await Response.Body.FlushAsync(ct);
    }

    /// <summary>POST /api/analysis/transcribe — transcribes audio via Groq Whisper.</summary>
    [HttpPost("transcribe")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Transcribe(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var text = await chatService.TranscribeAsync(
            stream,
            file.FileName ?? "recording.webm",
            file.ContentType ?? "audio/webm",
            ct);

        return text is null
            ? StatusCode(503, new { code = "TRANSCRIPTION_FAILED", message = "Transcription service unavailable." })
            : Ok(new { text });
    }
}
