using GridTrack.Application.Dtos;

namespace GridTrack.Presentation.Controllers.Analysis;

public sealed record ChatRequest(IEnumerable<ChatMessageDto> Messages, string CsvData);
