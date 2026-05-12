namespace GridTrack.Application.Abstractions.Validation;

public sealed record ValidationError(string PropertyName, string ErrorMessage);