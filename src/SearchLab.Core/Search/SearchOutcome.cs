namespace SearchLab.Core.Search;

/// <summary>Score is not comparable across backends.</summary>
public sealed record SearchHit(string PostingId, double Score, string Text);

public sealed record SearchOutcome(
    IReadOnlyList<SearchHit> Hits,
    long TotalHits,
    QueryTrace Trace,
    string? NextCursor);
