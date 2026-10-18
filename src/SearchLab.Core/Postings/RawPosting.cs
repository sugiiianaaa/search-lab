namespace SearchLab.Core.Postings;

/// <summary>Text is HTML, exactly as HN serves it.</summary>
public sealed record RawPosting(string Id, int ThreadId, string Text, DateTimeOffset PostedAt);
