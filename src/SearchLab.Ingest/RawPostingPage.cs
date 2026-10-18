using SearchLab.Core.Postings;

namespace SearchLab.Ingest;

public sealed record RawPostingPage(IReadOnlyList<RawPosting> Postings, bool IsLastPage);
