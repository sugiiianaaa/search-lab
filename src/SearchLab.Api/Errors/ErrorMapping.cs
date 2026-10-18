using SearchLab.Core.Results;

namespace SearchLab.Api.Errors;

public static class ErrorMapping
{
    private const string _problemTypeBase = "https://searchlab.local/problems/";

    public static int ToStatusCode(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped ErrorKind."),
    };

    public static IResult ToHttpResult(Error error) => Results.Problem(
        statusCode: ToStatusCode(error.Kind),
        title: ToTitle(error.Kind),
        detail: error.Message,
        type: _problemTypeBase + error.Code,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    private static string ToTitle(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => "Invalid request",
        ErrorKind.NotFound => "Not found",
        ErrorKind.Unavailable => "Search backend unavailable",
        ErrorKind.Timeout => "Search backend timed out",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped ErrorKind."),
    };
}
