using System.Text.Json.Serialization;

namespace SearchLab.Ingest;

internal sealed record AlgoliaPage<THit>(
    [property: JsonPropertyName("hits")] IReadOnlyList<THit> Hits,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("nbPages")] int NbPages);

internal sealed record AlgoliaCommentHit(
    [property: JsonPropertyName("objectID")] string ObjectId,
    [property: JsonPropertyName("comment_text")] string? CommentText,
    [property: JsonPropertyName("created_at_i")] long CreatedAtI,
    [property: JsonPropertyName("parent_id")] int ParentId);
