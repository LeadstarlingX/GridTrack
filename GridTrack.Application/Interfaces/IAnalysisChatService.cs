using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IAnalysisChatService
{
    Task<string?> AskAsync(string question, string csvContext, CancellationToken ct);

    IAsyncEnumerable<string> StreamAsync(string question, string csvContext, CancellationToken ct);

    Task<string?> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct);

    Task<byte[]?> GenerateReportAsync(IEnumerable<ChatMessageDto> messages, string csvContext, CancellationToken ct);
}
