using Lucene.Net.Util;

namespace SearchLab.Search.Lucene;

internal static class LuceneSchema
{
    public const LuceneVersion Version = LuceneVersion.LUCENE_48;

    public const string IdField = "id";
    public const string TextField = "text";
    public const string PostedAtField = "posted_at";
}
