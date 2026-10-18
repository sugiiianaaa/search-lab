using Microsoft.Extensions.Logging;

namespace SearchLab.Ingest;

// EventIds: 5000s information, 6000s warning, 7000s error, clear of SearchLog's range. Never renumber one.
public static partial class IngestLog
{
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Fetched Algolia page {Page} for thread {ThreadId}: hits={HitCount} kept={KeptCount} isLastPage={IsLastPage}")]
    public static partial void PageFetched(ILogger logger, int page, int threadId, int hitCount, int keptCount, bool isLastPage);

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Warning,
        Message = "Retrying '{Uri}' after {Reason}: attempt {Attempt} of {MaxAttempts}, waiting {DelaySeconds}s")]
    public static partial void RetryingRequest(ILogger logger, Uri? uri, string reason, int attempt, int maxAttempts, double delaySeconds);

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Error,
        Message = "Algolia unreachable for '{Uri}'")]
    public static partial void AlgoliaUnavailable(ILogger logger, string uri, Exception exception);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Error,
        Message = "Algolia did not respond to '{Uri}' within the configured timeout")]
    public static partial void AlgoliaTimedOut(ILogger logger, string uri);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Error,
        Message = "Algolia returned {StatusCode} for '{Uri}'. Body: {Body}")]
    public static partial void AlgoliaRejected(ILogger logger, string uri, int statusCode, string body);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Error,
        Message = "Algolia response for '{Uri}' did not match the expected shape. Body: {Body}")]
    public static partial void AlgoliaResponseUnreadable(ILogger logger, string uri, string body, Exception exception);

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Error,
        Message = "Algolia returned an empty body for '{Uri}'")]
    public static partial void AlgoliaResponseEmpty(ILogger logger, string uri);
}
