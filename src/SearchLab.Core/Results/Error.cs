namespace SearchLab.Core.Results;

public enum ErrorKind
{
    Validation,
    NotFound,
    Unavailable,
    Timeout,
}

/// <summary>Clients branch on Code, so never reword one. Message is shown to callers, so never put secrets in it.</summary>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    public static Error Validation(string code, string message) => new(ErrorKind.Validation, code, message);

    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);

    public static Error Unavailable(string code, string message) => new(ErrorKind.Unavailable, code, message);

    public static Error Timeout(string code, string message) => new(ErrorKind.Timeout, code, message);
}
