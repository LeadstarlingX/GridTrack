using GridTrack.Application.Dtos;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IExportReadService
{
    Task<ExportCsvResult> ExportDeliveriesAsync(
        string mode,
        DateTime? from,
        DateTime? to,
        IReadOnlyList<string>? days,
        int? fromHour,
        int? toHour,
        CancellationToken ct);
}
