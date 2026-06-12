namespace GridTrack.Application.Dtos;

public sealed record StatusBreakdownItemResponse(int Status, string Label, int Count);

public sealed record GetStatusBreakdownResponse(IReadOnlyList<StatusBreakdownItemResponse> Items);
