namespace GridTrack.Presentation.Abstractions.Api;

public record Error
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static readonly Error NullValue = new("Error.NullValue", "Null value was provided");
    
    public Error(string code, string message)
    {
        Code = code;
        Message = message;
    }

    public string Code { get; private set; }
    public string Message { get; private set; }
    
}
