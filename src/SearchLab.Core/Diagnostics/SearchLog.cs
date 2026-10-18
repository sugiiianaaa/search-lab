using Microsoft.Extensions.Logging;

namespace SearchLab.Core.Diagnostics;

// EventIds: 1000s information, 2000s warning, 3000s error. Never renumber one.
public static partial class SearchLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Search on {Backend} query={QueryText} tokens={TokenCount} parsed={ParsedQuery} rewritten={RewrittenQuery} hits={HitCount} duration={DurationMs}ms")]
    public static partial void SearchExecuted(
        ILogger logger,
        string backend,
        string queryText,
        int tokenCount,
        string parsedQuery,
        string rewrittenQuery,
        long hitCount,
        double durationMs);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Analyzer produced zero tokens on {Backend} for query={QueryText}. This query cannot match anything.")]
    public static partial void AnalyzerProducedNoTokens(ILogger logger, string backend, string queryText);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Zero results on {Backend} query={QueryText} tokens={Tokens}")]
    public static partial void ZeroResults(ILogger logger, string backend, string queryText, string tokens);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Query parse failed on {Backend} query={QueryText}: {Reason}")]
    public static partial void QueryParseFailed(ILogger logger, string backend, string queryText, string reason);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Error,
        Message = "Backend {Backend} unavailable")]
    public static partial void BackendUnavailable(ILogger logger, string backend, Exception exception);

}
