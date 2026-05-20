namespace GridTrack.Presentation.Abstractions.Api;

public class PagedResponse<T>
{
    public List<T> Items { get; set; } = default!;
    public string? NextCursor { get; set; }
    public int? TotalCount { get; set; }
}