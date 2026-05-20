using System.Text.Json.Serialization;

namespace GridTrack.Presentation.Abstractions.Api;

public sealed class ErrorDetails
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("statusCode")]
    public required int StatusCode { get; init; }

    [JsonPropertyName("validationErrors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? ValidationErrors { get; init; }

    [JsonPropertyName("traceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }
}