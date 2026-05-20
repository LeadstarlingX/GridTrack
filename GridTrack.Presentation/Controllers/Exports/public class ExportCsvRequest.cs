namespace GridTrack.Presentation.Controllers.Exports;

public class ExportCsvRequest
{
    public string Mode { get; set; } = null!;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<string>? Days { get; set; }
    public int? FromHour { get; set; }
    public int? ToHour { get; set; }
}