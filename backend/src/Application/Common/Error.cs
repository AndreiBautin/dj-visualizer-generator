namespace DjVisualizer.Application.Common;

public sealed record Error(string Code, string Message)
{
    public static Error Validation(string message) => new(ErrorCodes.Validation, message);

    public static Error NotFound(string message) => new(ErrorCodes.NotFound, message);

    public static Error Failure(string message) => new(ErrorCodes.Failure, message);
}
