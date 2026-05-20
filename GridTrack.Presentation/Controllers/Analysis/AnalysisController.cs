using GridTrack.Presentation.Abstractions.Api;
using Microsoft.AspNetCore.Mvc;

namespace GridTrack.Presentation.Controllers.Analysis;

[ApiController]
[Route("api/analysis")]
public class AnalysisController : ControllerBase
{
    /// <summary>
    /// AI chat analysis with CSV context
    /// </summary>
    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<ChatResponse>>> Chat([FromBody] ChatRequest request)
    {
        // Minimal implementation for system contract design
        // This would typically call into your application layer for AI/LLM processing

        var reply = $"Received {request.Messages.Count()} messages with CSV data of length {request.CsvData.Length}";

        var response = new ChatResponse
        {
            Reply = reply
        };

        return Ok(ApiResponse<ChatResponse>.Ok(response));
    }
}