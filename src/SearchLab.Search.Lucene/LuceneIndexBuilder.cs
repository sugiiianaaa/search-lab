using SearchLab.Core.Postings;
using SearchLab.Core.Results;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;

namespace SearchLab.Search.Lucene;

public sealed class LuceneIndexBuilder(string indexPath)
{
    /// <summary>Replaces whatever index is at the path. Returns how many postings were indexed.</summary>
    public Result<int> Build(IEnumerable<RawPosting> postings, CancellationToken cancellationToken)
    {
        try
        {
            using FSDirectory directory = FSDirectory.Open(indexPath);
            using PostingAnalyzer analyzer = new();

            IndexWriterConfig config = new(LuceneSchema.Version, analyzer)
            {
                Similarity = new BM25Similarity(),
                OpenMode = OpenMode.CREATE,
            };

            using IndexWriter writer = new(directory, config);
            int count = 0;

            foreach (RawPosting posting in postings)
            {
                cancellationToken.ThrowIfCancellationRequested();

                writer.AddDocument(
                [
                    new StringField(LuceneSchema.IdField, posting.Id, Field.Store.YES),
                    new TextField(LuceneSchema.TextField, posting.Text, Field.Store.YES),
                    new Int64Field(LuceneSchema.PostedAtField, posting.PostedAt.ToUnixTimeSeconds(), Field.Store.YES),
                ]);

                count++;
            }

            writer.Commit();

            return count;
        }
        catch (IOException ex)
        {
            return Error.Unavailable("index.write_failed", $"Could not write to the index at '{indexPath}': {ex.Message}");
        }
    }
}
