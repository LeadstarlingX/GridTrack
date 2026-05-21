using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Export;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Exports;

[ApiController]
[Route("api/export")]
public class ExportController(IMessageBus bus) : ControllerBase
{
    [HttpPost("csv")]
    public async Task<IActionResult> ExportCsv(
        [FromBody] ExportCsvRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ExportCsvResult>(
            new ExportCsvCommand(
                request.Mode,
                request.From,
                request.To,
                request.Days,
                request.FromHour,
                request.ToHour),
            ct);

        return File(result!.CsvStream, "text/csv", result.FileName);
    }
}
