namespace SearchLab.Core.Search;

/// <summary>Already validated. When Cursor is set, Skip is ignored.</summary>
public sealed record SearchQuery(string Text, int Skip, int Take, string? Cursor = null);
