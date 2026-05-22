using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Dtos;

namespace GridTrack.Application.UseCases.Export;

public sealed record ExportCsvCommand(
    string Mode,
    DateTime? From,
    DateTime? To,
    IReadOnlyList<string>? Days,
    int? FromHour,
    int? ToHour);

public sealed class ExportCsvHandler
{
    public Task<ExportCsvResult> Handle(
        ExportCsvCommand command,
        IExportReadService exportReadService,
        CancellationToken ct)
        => exportReadService.ExportDeliveriesAsync(
            command.Mode,
            command.From,
            command.To,
            command.Days,
            command.FromHour,
            command.ToHour,
            ct);
}
