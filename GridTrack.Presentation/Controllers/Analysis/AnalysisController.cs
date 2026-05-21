using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Analysis;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Analysis;

[ApiController]
[Route("api/analysis")]
public class AnalysisController(IMessageBus bus) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ChatResponse>(
            new AnalysisChatQuery(request.Messages, request.CsvData),
            ct);

        return Ok(result);
    }
}
