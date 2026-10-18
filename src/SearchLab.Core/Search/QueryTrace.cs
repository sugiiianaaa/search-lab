namespace SearchLab.Core.Search;

public sealed record PhaseTiming(string Phase, TimeSpan Duration);

public sealed record ExplanationNode(double Value, string Description, IReadOnlyList<ExplanationNode> Children);

/// <summary>RewrittenQuery is what actually executed, and often differs from ParsedQuery.</summary>
public sealed record QueryTrace(
    string Backend,
    string QueryText,
    IReadOnlyList<string> AnalyzedTokens,
    string ParsedQuery,
    string RewrittenQuery,
    IReadOnlyList<PhaseTiming> Phases,
    IReadOnlyDictionary<string, ExplanationNode> Explanations);
