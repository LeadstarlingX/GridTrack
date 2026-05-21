namespace GridTrack.Presentation.Controllers.Exports;

public sealed record ExportCsvRequest(
    string Mode,
    DateTime? From,
    DateTime? To,
    IReadOnlyList<string>? Days,
    int? FromHour,
    int? ToHour);
