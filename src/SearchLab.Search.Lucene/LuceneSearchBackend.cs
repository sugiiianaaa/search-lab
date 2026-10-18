using System.Globalization;
using SearchLab.Core.Diagnostics;
using SearchLab.Core.Results;
using SearchLab.Core.Search;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Microsoft.Extensions.Logging;

namespace SearchLab.Search.Lucene;

public sealed class LuceneSearchBackend(string indexPath, ILogger<LuceneSearchBackend> logger) : ISearchBackend
{
    private const string _backendName = "lucene";

    private readonly string _indexPath = indexPath;
    private readonly ILogger<LuceneSearchBackend> _logger = logger;
    private readonly CursorCodec<LuceneCursor> _cursorCodec = new(_backendName);

    public string Name => _backendName;

    public Task<Result<SearchOutcome>> SearchAsync(SearchQuery query, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<Result<SearchOutcome>>(cancellationToken);
        }

        return Task.FromResult(Search(query));
    }

    public Task<Result<IReadOnlyList<string>>> AnalyzeAsync(string text, CancellationToken cancellationToken)
    {
        using PostingAnalyzer analyzer = new();

        return Task.FromResult(Result<IReadOnlyList<string>>.Ok(analyzer.Tokenize(text)));
    }

    private Result<SearchOutcome> Search(SearchQuery query)
    {
        Result<ScoreDoc?> resumeAfter = ReadCursor(query.Cursor);
        if (!resumeAfter.IsSuccess)
        {
            return resumeAfter.Error;
        }

        using FSDirectory directory = FSDirectory.Open(_indexPath);

        Result<DirectoryReader> opened = OpenIndex(directory);
        if (!opened.IsSuccess)
        {
            return opened.Error;
        }

        using DirectoryReader reader = opened.Value;
        using PostingAnalyzer analyzer = new();
        PhaseRecorder recorder = new(Name);

        List<string> tokens = recorder.Measure(Telemetry.Phases.Analyze, () => analyzer.Tokenize(query.Text));

        Result<Query> parsed = Parse(query.Text, analyzer, recorder);
        if (!parsed.IsSuccess)
        {
            return parsed.Error;
        }

        IndexSearcher searcher = new(reader) { Similarity = new BM25Similarity() };

        ResultPage page = recorder.Measure(Telemetry.Phases.Score, () => FindPage(searcher, parsed.Value, query, resumeAfter.Value));
        List<SearchHit> hits = recorder.Measure(Telemetry.Phases.Hydrate, () => LoadHits(searcher, page.Docs));
        Dictionary<string, ExplanationNode> explanations = recorder.Measure(Telemetry.Phases.Explain, () => Explain(searcher, parsed.Value, page.Docs, hits));

        QueryTrace trace = new(
            Backend: Name,
            QueryText: query.Text,
            AnalyzedTokens: tokens,
            ParsedQuery: parsed.Value.ToString(),
            RewrittenQuery: parsed.Value.Rewrite(reader).ToString(),
            Phases: recorder.Timings,
            Explanations: explanations);

        Report(trace, hits.Count, page.TotalHits);

        return new SearchOutcome(hits, page.TotalHits, trace, NextCursor(page));
    }

    private Result<ScoreDoc?> ReadCursor(string? cursor)
    {
        if (cursor is null)
        {
            return Result<ScoreDoc?>.Ok(null);
        }

        Result<LuceneCursor> decoded = _cursorCodec.Decode(cursor);
        if (!decoded.IsSuccess)
        {
            return decoded.Error;
        }

        return new ScoreDoc(decoded.Value.Doc, decoded.Value.Score);
    }

    private Result<DirectoryReader> OpenIndex(FSDirectory directory)
    {
        try
        {
            return DirectoryReader.Open(directory);
        }
        catch (IOException ex)
        {
            SearchLog.BackendUnavailable(_logger, Name, ex);

            return Error.Unavailable("search.index_unavailable", "Could not open the search index. Has it been built?");
        }
    }

    private Result<Query> Parse(string text, PostingAnalyzer analyzer, PhaseRecorder recorder)
    {
        QueryParser parser = new(LuceneSchema.Version, LuceneSchema.TextField, analyzer);

        try
        {
            return recorder.Measure(Telemetry.Phases.Parse, () => parser.Parse(text));
        }
        catch (ParseException ex)
        {
            string reason = ex.Message.Split('\n')[0].Trim();

            SearchLog.QueryParseFailed(_logger, Name, text, reason);

            return Error.Validation("search.query_parse_failed", reason);
        }
    }

    private static ResultPage FindPage(IndexSearcher searcher, Query query, SearchQuery request, ScoreDoc? resumeAfter)
    {
        // One hit more than the page holds, to know whether a next page exists.
        int skip = resumeAfter is null ? request.Skip : 0;

        TopDocs topDocs = resumeAfter is null
            ? searcher.Search(query, skip + request.Take + 1)
            : searcher.SearchAfter(resumeAfter, query, request.Take + 1);

        ScoreDoc[] docs = [.. topDocs.ScoreDocs.Skip(skip).Take(request.Take)];
        bool hasNextPage = topDocs.ScoreDocs.Length > skip + docs.Length;

        return new ResultPage(docs, topDocs.TotalHits, hasNextPage);
    }

    private static List<SearchHit> LoadHits(IndexSearcher searcher, ScoreDoc[] docs) =>
        [.. docs.Select(scoreDoc =>
        {
            Document document = searcher.Doc(scoreDoc.Doc);

            return new SearchHit(
                document.Get(LuceneSchema.IdField, CultureInfo.InvariantCulture),
                scoreDoc.Score,
                document.Get(LuceneSchema.TextField, CultureInfo.InvariantCulture));
        })];

    private static Dictionary<string, ExplanationNode> Explain(IndexSearcher searcher, Query query, ScoreDoc[] docs, List<SearchHit> hits) =>
        hits.Zip(docs).ToDictionary(
            pair => pair.First.PostingId,
            pair => ToExplanationNode(searcher.Explain(query, pair.Second.Doc)));

    private static ExplanationNode ToExplanationNode(Explanation explanation) => new(
        explanation.Value,
        explanation.Description,
        [.. (explanation.GetDetails() ?? []).Select(ToExplanationNode)]);

    private string? NextCursor(ResultPage page)
    {
        if (!page.HasNextPage)
        {
            return null;
        }

        ScoreDoc last = page.Docs[^1];

        return _cursorCodec.Encode(new LuceneCursor(last.Doc, last.Score));
    }

    private void Report(QueryTrace trace, int hitCount, long totalHits)
    {
        if (trace.AnalyzedTokens.Count == 0)
        {
            SearchLog.AnalyzerProducedNoTokens(_logger, Name, trace.QueryText);
        }
        else if (hitCount == 0)
        {
            Telemetry.Metrics.ZeroResultSearches.Add(1, new KeyValuePair<string, object?>("backend", Name));
            SearchLog.ZeroResults(_logger, Name, trace.QueryText, string.Join(",", trace.AnalyzedTokens));
        }

        double durationMs = trace.Phases.Sum(phase => phase.Duration.TotalMilliseconds);

        SearchLog.SearchExecuted(
            _logger, Name, trace.QueryText, trace.AnalyzedTokens.Count, trace.ParsedQuery, trace.RewrittenQuery, totalHits, durationMs);
    }

    private sealed record ResultPage(ScoreDoc[] Docs, long TotalHits, bool HasNextPage);

    // Doc is a position in the current reader, not a stable id: a cursor can skip or repeat
    // a hit if the index changes between pages.
    private sealed record LuceneCursor(int Doc, float Score);
}
