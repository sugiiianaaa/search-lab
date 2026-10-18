using SearchLab.Api.Contracts.V1;
using SearchLab.Api.Errors;
using SearchLab.Core.Results;
using SearchLab.Core.Search;

namespace SearchLab.Api.Endpoints;

public static class SearchEndpoints
{
    private const string _defaultBackend = "lucene";

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/v1").WithTags("Search");

        group.MapGet("/search", SearchAsync)
            .WithName("Search")
            .WithSummary("Run a query and return both the results and how they were produced.");

        group.MapGet("/analyze", AnalyzeAsync)
            .WithName("Analyze")
            .WithSummary("Show what the analyzer chain does to a piece of text. No index required.");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        [AsParameters] SearchRequest request,
        IEnumerable<ISearchBackend> backends,
        CancellationToken cancellationToken)
    {
        Result<SearchQuery> query = Validate(request);
        if (!query.IsSuccess)
        {
            return ErrorMapping.ToHttpResult(query.Error);
        }

        Result<ISearchBackend> backend = FindBackend(backends, request.Backend);
        if (!backend.IsSuccess)
        {
            return ErrorMapping.ToHttpResult(backend.Error);
        }

        Result<SearchOutcome> outcome = await backend.Value.SearchAsync(query.Value, cancellationToken);
        if (!outcome.IsSuccess)
        {
            return ErrorMapping.ToHttpResult(outcome.Error);
        }

        return Results.Ok(ToResponse(outcome.Value, query.Value));
    }

    private static async Task<IResult> AnalyzeAsync(
        string text,
        string? backend,
        IEnumerable<ISearchBackend> backends,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ErrorMapping.ToHttpResult(
                Error.Validation("analyze.text_empty", "Provide text to analyze in the 'text' parameter."));
        }

        Result<ISearchBackend> found = FindBackend(backends, backend);
        if (!found.IsSuccess)
        {
            return ErrorMapping.ToHttpResult(found.Error);
        }

        Result<IReadOnlyList<string>> tokens = await found.Value.AnalyzeAsync(text, cancellationToken);
        if (!tokens.IsSuccess)
        {
            return ErrorMapping.ToHttpResult(tokens.Error);
        }

        return Results.Ok(new { backend = found.Value.Name, input = text, tokens = tokens.Value });
    }

    private static Result<SearchQuery> Validate(SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Q))
        {
            return Error.Validation("search.query_empty", "Parameter 'q' is required and cannot be blank.");
        }

        if (request.Q.Length > SearchLimits.MaxQueryLength)
        {
            return Error.Validation(
                "search.query_too_long",
                $"Parameter 'q' must be {SearchLimits.MaxQueryLength} characters or fewer.");
        }

        if (request.Skip > SearchLimits.MaxSkip)
        {
            return Error.Validation(
                "search.skip_too_large",
                $"Parameter 'skip' must be {SearchLimits.MaxSkip} or less. Page deeper with 'cursor'.");
        }

        return new SearchQuery(
            Text: request.Q,
            Skip: Math.Max(0, request.Skip ?? 0),
            Take: Math.Clamp(request.Take ?? SearchLimits.DefaultTake, 1, SearchLimits.MaxTake),
            Cursor: string.IsNullOrWhiteSpace(request.Cursor) ? null : request.Cursor);
    }

    private static Result<ISearchBackend> FindBackend(IEnumerable<ISearchBackend> backends, string? name)
    {
        string wanted = string.IsNullOrWhiteSpace(name) ? _defaultBackend : name;

        ISearchBackend? match = backends.FirstOrDefault(
            backend => string.Equals(backend.Name, wanted, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return Error.NotFound(
                "search.unknown_backend",
                $"No backend named '{wanted}'. Available: {string.Join(", ", backends.Select(backend => backend.Name))}.");
        }

        return Result<ISearchBackend>.Ok(match);
    }

    private static SearchResponse ToResponse(SearchOutcome outcome, SearchQuery query) => new(
        Hits: [.. outcome.Hits.Select(hit => new SearchHitResponse(hit.PostingId, hit.Score, hit.Text))],
        TotalHits: outcome.TotalHits,
        Skip: query.Skip,
        Take: query.Take,
        NextCursor: outcome.NextCursor,
        Trace: new QueryTraceResponse(
            Backend: outcome.Trace.Backend,
            QueryText: outcome.Trace.QueryText,
            AnalyzedTokens: outcome.Trace.AnalyzedTokens,
            ParsedQuery: outcome.Trace.ParsedQuery,
            RewrittenQuery: outcome.Trace.RewrittenQuery,
            Phases: [.. outcome.Trace.Phases.Select(phase => new PhaseTimingResponse(phase.Phase, phase.Duration.TotalMilliseconds))],
            Explanations: outcome.Trace.Explanations.ToDictionary(entry => entry.Key, entry => ToResponse(entry.Value))));

    private static ExplanationResponse ToResponse(ExplanationNode node) => new(
        node.Value,
        node.Description,
        [.. node.Children.Select(ToResponse)]);
}
