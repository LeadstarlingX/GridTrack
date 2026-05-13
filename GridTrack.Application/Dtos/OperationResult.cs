using GridTrack.Domain.Abstractions;

namespace GridTrack.Application.Dtos;

public sealed record OperationResult(bool IsSuccess, Error? Error)
{
    public static OperationResult Success() => new(true, null);

    public static OperationResult Failure(Error error) => new(false, error);

    public static OperationResult From(Result result)
        => result.IsSuccess ? Success() : Failure(result.Error);
}
