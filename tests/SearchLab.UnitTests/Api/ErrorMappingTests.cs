using SearchLab.Api.Errors;
using SearchLab.Core.Results;

namespace SearchLab.UnitTests.Api;

public sealed class ErrorMappingTests
{
    [Fact]
    public void EveryErrorKind_MapsToAStatusCode()
    {
        foreach (ErrorKind kind in Enum.GetValues<ErrorKind>())
        {
            int status = ErrorMapping.ToStatusCode(kind);

            Assert.InRange(status, 400, 599);
        }
    }

    [Theory]
    [InlineData(ErrorKind.Validation, 400)]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.Unavailable, 503)]
    [InlineData(ErrorKind.Timeout, 504)]
    public void ErrorKind_MapsToExpectedStatus(ErrorKind kind, int expected)
        => Assert.Equal(expected, ErrorMapping.ToStatusCode(kind));

    [Fact]
    public void ValidationErrors_AreNotServerErrors()
    {
        int status = ErrorMapping.ToStatusCode(ErrorKind.Validation);

        Assert.True(status < 500, $"Validation mapped to {status}; a bad request is the caller's fault.");
    }
}
