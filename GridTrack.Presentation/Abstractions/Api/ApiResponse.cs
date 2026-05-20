using System.Text.Json.Serialization;

namespace GridTrack.Presentation.Abstractions.Api;

public sealed class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; private init; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; private init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorDetails? Error { get; private init; }

    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseMetadata? Meta { get; private init; }

    private ApiResponse() { }

    public static ApiResponse<T> Ok(T data, ResponseMetadata? meta = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Meta = meta
        };
    }

    public static ApiResponse<T> Fail(
        string code,
        string message,
        int statusCode = 400,
        Dictionary<string, string[]>? validationErrors = null,
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Error = new ErrorDetails
            {
                Code = code,
                Message = message,
                StatusCode = statusCode,
                ValidationErrors = validationErrors,
                TraceId = traceId,
                Timestamp = DateTime.UtcNow
            }
        };
    }

    public static ApiResponse<T> Fail(Error error, int statusCode = 400, string? traceId = null)
    {
        return Fail(error.Code, error.Message, statusCode, traceId: traceId);
    }
}

public sealed class ApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; private init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorDetails? Error { get; private init; }

    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseMetadata? Meta { get; private init; }

    private ApiResponse() { }

    public static ApiResponse Ok(ResponseMetadata? meta = null)
    {
        return new ApiResponse { Success = true, Meta = meta };
    }

    public static ApiResponse Fail(string code, string message, int statusCode = 400, string? traceId = null)
    {
        return new ApiResponse
        {
            Success = false,
            Error = new ErrorDetails
            {
                Code = code,
                Message = message,
                StatusCode = statusCode,
                TraceId = traceId,
                Timestamp = DateTime.UtcNow
            }
        };
    }
}