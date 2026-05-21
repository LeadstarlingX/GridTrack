namespace GridTrack.Application.Dtos;

public sealed record ExportCsvResult(Stream CsvStream, string FileName);
