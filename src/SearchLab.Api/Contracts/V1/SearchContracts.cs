using Microsoft.AspNetCore.Mvc;

namespace SearchLab.Api.Contracts.V1;

/// <summary>Query-string parameters for GET /v1/search.</summary>
/// <param name="Q">The user's raw query text.</param>
/// <param name="Backend">Which backend to run against. Defaults to lucene.</param>
/// <param name="Skip">Paging offset, at most SearchLimits.MaxSkip. Ignored once Cursor is set.</param>
/// <param name="Take">Page size.</param>
/// <param name="Cursor">Opaque token from a previous response's NextCursor, to page deeper than Skip/Take reaches.</param>
public sealed record SearchRequest(
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "backend")] string? Backend,
    [FromQuery(Name = "skip")] int? Skip,
    [FromQuery(Name = "take")] int? Take,
    [FromQuery(Name = "cursor")] string? Cursor);

public static class SearchLimits
{
    public const int MaxTake = 100;
    public const int DefaultTake = 20;
    public const int MaxQueryLength = 512;
    public const int MaxSkip = 1000;
}

/// <summary>One matching posting.</summary>
/// <param name="Id">Posting id. Joins to QueryTraceResponse.Explanations.</param>
/// <param name="Score">Score from the backend that ran. Not comparable across backends.</param>
/// <param name="Text">The posting's text, as HTML.</param>
public sealed record SearchHitResponse(string Id, double Score, string Text);

/// <summary>A node of the score explanation tree.</summary>
/// <param name="Value">This node's score contribution.</param>
/// <param name="Description">The backend's own wording.</param>
/// <param name="Children">Inputs to this node.</param>
public sealed record ExplanationResponse(
    double Value,
    string Description,
    IReadOnlyList<ExplanationResponse> Children);

/// <summary>One phase of execution and how long it took.</summary>
/// <param name="Phase">Phase name, matching the span name in the trace.</param>
/// <param name="Milliseconds">Duration.</param>
public sealed record PhaseTimingResponse(string Phase, double Milliseconds);

/// <summary>How the query executed. Part of every response, not behind a debug flag.</summary>
/// <param name="Backend">Which backend produced this.</param>
/// <param name="QueryText">What the user typed.</param>
/// <param name="AnalyzedTokens">What the analyzer produced. Empty here explains an empty result set.</param>
/// <param name="ParsedQuery">The parsed query as the backend renders it.</param>
/// <param name="RewrittenQuery">The query after rewriting. The form that actually executed.</param>
/// <param name="Phases">Per-phase timings, in execution order.</param>
/// <param name="Explanations">Score breakdown per hit id.</param>
public sealed record QueryTraceResponse(
    string Backend,
    string QueryText,
    IReadOnlyList<string> AnalyzedTokens,
    string ParsedQuery,
    string RewrittenQuery,
    IReadOnlyList<PhaseTimingResponse> Phases,
    IReadOnlyDictionary<string, ExplanationResponse> Explanations);

/// <summary>The 200 body of GET /v1/search.</summary>
/// <param name="Hits">This page of results.</param>
/// <param name="TotalHits">Total matches across all pages.</param>
/// <param name="Skip">Offset actually applied, after clamping.</param>
/// <param name="Take">Page size actually applied, after clamping.</param>
/// <param name="NextCursor">Pass as `cursor` to fetch the page after this one. Null when there is nothing more.</param>
/// <param name="Trace">How this query executed.</param>
public sealed record SearchResponse(
    IReadOnlyList<SearchHitResponse> Hits,
    long TotalHits,
    int Skip,
    int Take,
    string? NextCursor,
    QueryTraceResponse Trace);
