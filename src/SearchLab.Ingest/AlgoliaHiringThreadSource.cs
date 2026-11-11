using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchLab.Core.Results;

namespace SearchLab.Ingest;

public sealed class AlgoliaHiringThreadSource(HttpClient httpClient, ILogger<AlgoliaHiringThreadSource> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AlgoliaHiringThreadSource> _logger = logger;

    // whoishiring posts about 3 stories a month, so a small page covers many hiring threads.
    private const int _hitsPerPage = 50;
    private const int _maxLoggedBodyLength = 500;

    public async Task<Result<IReadOnlyList<HiringThread>>> FetchLatestAsync(int count, CancellationToken cancellationToken)
    {
        var threads = new List<HiringThread>();
        int page = 0;

        while (threads.Count < count)
        {
            string uri = $"search_by_date?tags=story,author_whoishiring&page={page}&hitsPerPage={_hitsPerPage}";

            using HttpResponseMessage response = await _httpClient.GetAsync(uri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Error.Unavailable("ingest.source_bad_response", $"Algolia returned a non-success status code for '{uri}': {response.StatusCode}");
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            Result<AlgoliaPage<AlgoliaThreadHit>> parsed = Parse(uri, body);
            if (!parsed.IsSuccess)
            {
                return parsed.Error;
            }

            foreach (AlgoliaThreadHit hit in parsed.Value.Hits)
            {
                if (!HiringThreadFilter.IsWhoIsHiringAuthor(hit.Author) ||
                    !HiringThreadFilter.IsHiringThread(hit.Title))
                {
                    continue;
                }

                DateTime createdAt = DateTimeOffset.FromUnixTimeSeconds(hit.CreatedAtI).UtcDateTime;

                threads.Add(new HiringThread(hit.StoryId, hit.Title, createdAt.Year, createdAt.Month));

                if (threads.Count >= count)
                {
                    break;
                }
            }

            page++;

            if (page >= parsed.Value.NbPages)
            {
                break;
            }
        }

        return threads;
    }

    private Result<AlgoliaPage<AlgoliaThreadHit>> Parse(string uri, string body)
    {
        AlgoliaPage<AlgoliaThreadHit>? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<AlgoliaPage<AlgoliaThreadHit>>(body);
        }
        catch (JsonException ex)
        {
            IngestLog.AlgoliaResponseUnreadable(_logger, uri, Truncate(body), ex);

            return Error.Unavailable("ingest.source_bad_response", $"Algolia response for '{uri}' did not match the expected shape.");
        }

        if (parsed is null || parsed.Hits is null || parsed.NbPages < 1)
        {
            IngestLog.AlgoliaResponseEmpty(_logger, uri);

            return Error.Unavailable("ingest.source_bad_response", $"Algolia returned an empty body for '{uri}'.");
        }

        return parsed;
    }

    private static string Truncate(string body) =>
       body.Length > _maxLoggedBodyLength ? body[.._maxLoggedBodyLength] + "…" : body;
}
