using System.Text.Json;
using SearchLab.Core.Postings;
using SearchLab.Core.Results;
using Microsoft.Extensions.Logging;

namespace SearchLab.Ingest;

public sealed class AlgoliaPostingSource(HttpClient httpClient, ILogger<AlgoliaPostingSource> logger)
{
    private const int _postingsPerPage = 30;
    private const int _maxLoggedBodyLength = 500;

    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AlgoliaPostingSource> _logger = logger;

    public async Task<Result<RawPostingPage>> FetchPageAsync(int page, int threadId, CancellationToken cancellationToken)
    {
        string uri = CommentsUri(page, threadId);

        Result<string> body = await DownloadAsync(uri, cancellationToken);
        if (!body.IsSuccess)
        {
            return body.Error;
        }

        Result<AlgoliaPage<AlgoliaCommentHit>> algoliaPage = Parse(uri, body.Value);
        if (!algoliaPage.IsSuccess)
        {
            return algoliaPage.Error;
        }

        return KeepPostings(page, threadId, algoliaPage.Value);
    }

    private async Task<Result<string>> DownloadAsync(string uri, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(uri, cancellationToken);

            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                IngestLog.AlgoliaRejected(_logger, uri, (int)response.StatusCode, Truncate(body));

                return Error.Unavailable("ingest.source_rejected", $"Algolia returned {(int)response.StatusCode} for '{uri}'.");
            }

            return body;
        }
        catch (HttpRequestException ex)
        {
            IngestLog.AlgoliaUnavailable(_logger, uri, ex);

            return Error.Unavailable("ingest.source_unavailable", $"Could not reach Algolia for '{uri}': {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout. A caller's cancellation keeps propagating.
            IngestLog.AlgoliaTimedOut(_logger, uri);

            return Error.Timeout("ingest.source_timeout", $"Algolia did not respond to '{uri}' in time.");
        }
    }

    private Result<AlgoliaPage<AlgoliaCommentHit>> Parse(string uri, string body)
    {
        AlgoliaPage<AlgoliaCommentHit>? algoliaPage;

        try
        {
            algoliaPage = JsonSerializer.Deserialize<AlgoliaPage<AlgoliaCommentHit>>(body);
        }
        catch (JsonException ex)
        {
            IngestLog.AlgoliaResponseUnreadable(_logger, uri, Truncate(body), ex);

            return Error.Unavailable("ingest.source_bad_response", $"Algolia's response for '{uri}' did not match the expected shape.");
        }

        if (algoliaPage is null)
        {
            IngestLog.AlgoliaResponseEmpty(_logger, uri);

            return Error.Unavailable("ingest.source_bad_response", $"Algolia returned an empty body for '{uri}'.");
        }

        return algoliaPage;
    }

    private RawPostingPage KeepPostings(int page, int threadId, AlgoliaPage<AlgoliaCommentHit> algoliaPage)
    {
        List<RawPosting> postings = [];

        foreach (AlgoliaCommentHit hit in algoliaPage.Hits)
        {
            if (ToPosting(hit, threadId) is RawPosting posting)
            {
                postings.Add(posting);
            }
        }

        bool isLastPage = algoliaPage.Hits.Count == 0 || algoliaPage.Page >= algoliaPage.NbPages - 1;

        IngestLog.PageFetched(_logger, page, threadId, algoliaPage.Hits.Count, postings.Count, isLastPage);

        return new RawPostingPage(postings, isLastPage);
    }

    private static RawPosting? ToPosting(AlgoliaCommentHit hit, int threadId)
    {
        bool isDeletedOrFlagged = hit.CommentText is null;
        bool isReply = hit.ParentId != threadId;

        if (isDeletedOrFlagged || isReply)
        {
            return null;
        }

        return new RawPosting(
            hit.ObjectId,
            threadId,
            hit.CommentText!,
            DateTimeOffset.FromUnixTimeSeconds(hit.CreatedAtI));
    }

    private static string CommentsUri(int page, int threadId) =>
        $"search_by_date?tags=comment,story_{threadId}&page={page}&hitsPerPage={_postingsPerPage}";

    private static string Truncate(string body) =>
        body.Length > _maxLoggedBodyLength ? body[.._maxLoggedBodyLength] + "…" : body;
}
