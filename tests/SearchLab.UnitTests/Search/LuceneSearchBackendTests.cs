using SearchLab.Core.Postings;
using SearchLab.Core.Diagnostics;
using SearchLab.Core.Results;
using SearchLab.Core.Search;
using SearchLab.Search.Lucene;
using Microsoft.Extensions.Logging.Abstractions;

namespace SearchLab.UnitTests.Search;

public sealed class LuceneIndexFixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "searchlab-lucene-tests-" + Guid.NewGuid());

    public LuceneIndexFixture()
    {
        IndexPath = Path.Combine(_root, "index");

        Result<int> built = new LuceneIndexBuilder(IndexPath).Build(
            [
                new RawPosting("csharp-post", 1, "Senior C# Engineer, remote friendly.", DateTimeOffset.UtcNow),
                new RawPosting("engineer-post", 1, "Engineer.", DateTimeOffset.UtcNow),
                new RawPosting("unrelated-post", 1, "Warehouse associate, forklift certified.", DateTimeOffset.UtcNow),
            ],
            CancellationToken.None);

        if (!built.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to build the test index: {built.Error.Message}");
        }
    }

    public string IndexPath { get; }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}

public sealed class LuceneSearchBackendTests(LuceneIndexFixture fixture) : IClassFixture<LuceneIndexFixture>
{
    private readonly LuceneSearchBackend _backend = new(fixture.IndexPath, NullLogger<LuceneSearchBackend>.Instance);

    [Fact]
    public async Task AnalyzeAsync_TechTermAndPlural_RunsThePostingAnalyzer()
    {
        Result<IReadOnlyList<string>> result = await _backend.AnalyzeAsync("Senior C# Engineers", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["senior", "csharp", "engineer"], result.Value);
    }

    [Fact]
    public async Task SearchAsync_TechTerm_FindsPostingIndexedWithTheSameAnalyzer()
    {
        Result<SearchOutcome> result = await _backend.SearchAsync(new SearchQuery("c#", Skip: 0, Take: 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["csharp"], result.Value.Trace.AnalyzedTokens);
        Assert.Equal(["csharp-post"], result.Value.Hits.Select(hit => hit.PostingId));
    }

    [Fact]
    public async Task SearchAsync_AnyQuery_ScoresWithBm25()
    {
        Result<SearchOutcome> result = await _backend.SearchAsync(new SearchQuery("engineer", Skip: 0, Take: 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.Trace.Explanations);

        foreach (ExplanationNode root in result.Value.Trace.Explanations.Values)
        {
            Assert.Contains("BM25Similarity", root.Description);

            List<ExplanationNode> nodes = [.. Flatten(root)];
            Assert.Contains(nodes, node => node.Description.Contains("parameter k1", StringComparison.Ordinal));
            Assert.Contains(nodes, node => node.Description.Contains("parameter b", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SearchAsync_AnyQuery_TimesEachStepAsItsOwnPhase()
    {
        Result<SearchOutcome> result = await _backend.SearchAsync(new SearchQuery("engineer", Skip: 0, Take: 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [Telemetry.Phases.Analyze, Telemetry.Phases.Parse, Telemetry.Phases.Score, Telemetry.Phases.Hydrate, Telemetry.Phases.Explain],
            result.Value.Trace.Phases.Select(phase => phase.Phase));
    }

    [Fact]
    public async Task SearchAsync_WithCursor_ResumesPastThePreviousPage()
    {
        Result<SearchOutcome> first = await _backend.SearchAsync(new SearchQuery("engineer", Skip: 0, Take: 1), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Single(first.Value.Hits);
        Assert.NotNull(first.Value.NextCursor);

        Result<SearchOutcome> second = await _backend.SearchAsync(
            new SearchQuery("engineer", Skip: 0, Take: 1, Cursor: first.Value.NextCursor),
            CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Single(second.Value.Hits);
        Assert.NotEqual(first.Value.Hits[0].PostingId, second.Value.Hits[0].PostingId);
        Assert.Null(second.Value.NextCursor);
    }

    [Fact]
    public async Task SearchAsync_WithCursorFromAnotherBackend_FailsValidation()
    {
        string foreignCursor = new CursorCodec<string>("postgres").Encode("irrelevant-state");

        Result<SearchOutcome> result = await _backend.SearchAsync(
            new SearchQuery("engineer", Skip: 0, Take: 1, Cursor: foreignCursor),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("search.cursor_wrong_backend", result.Error.Code);
    }

    private static IEnumerable<ExplanationNode> Flatten(ExplanationNode node) =>
        node.Children.SelectMany(Flatten).Prepend(node);
}
