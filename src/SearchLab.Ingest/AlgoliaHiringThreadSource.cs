using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchLab.Core.Results;
using SearchLab.Ingest;

namespace SearchLab.Ingest;

public sealed class AlgoliaHiringThreadSource(HttpClient httpClient, ILogger<AlgoliaHiringThreadSource> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AlgoliaHiringThreadSource> _logger = logger;

    private readonly int _maxHitsPerPage = 1000; // algolia has a limit of 1000 hits per page
    private const int _maxLoggedBodyLength = 500;

    public async Task<Result<IReadOnlyList<HiringThread>>> FetchLatestAsync(int count, CancellationToken cancellationToken)
    {
        int threadFound = 0;
        int page = 0;
        var threads = new List<HiringThread>();

        while (threadFound < count)
        {
            string uri = $"search_by_date?tags=story,author_whoishiring&page={page}&hitsPerPage={_maxHitsPerPage}";

            HttpResponseMessage response = await _httpClient.GetAsync(
                uri,
                cancellationToken
            );

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return Error.Unavailable("ingest.source_bad_response", $"Algolia returned a non-success status code for '{uri}': {response.StatusCode}");
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            Result<AlgoliaPage<AlgoliaThreadHit>> result = Parse(uri, body);

            if (!result.IsSuccess)
            {
                return result.Error;
            }

            foreach (AlgoliaThreadHit hit in result.Value.Hits)
            {
                if (!HiringThreadFilter.IsWhoIsHiringAuthor(hit.Author) ||
                    !HiringThreadFilter.IsHiringThread(hit.Title))
                {
                    continue;
                }

                var dateTime = DateTimeOffset.FromUnixTimeSeconds(hit.CreatedAtI).UtcDateTime;

                threads.Add(new HiringThread(
                    hit.StoryId,
                    hit.Title,
                    dateTime.Year,
                    dateTime.Month
                ));

                threadFound++;

                if (threadFound >= count)
                {
                    break;
                }
            }

            page++;

            if (page >= result.Value.NbPages)
            {
                break;
            }
        }

        return threads;
    }

    private Result<AlgoliaPage<AlgoliaThreadHit>> Parse(string uri, string body)
    {
        AlgoliaPage<AlgoliaThreadHit>? page;

        try
        {
            page = JsonSerializer.Deserialize<AlgoliaPage<AlgoliaThreadHit>>(body);
        }
        catch (JsonException ex)
        {
            IngestLog.AlgoliaResponseUnreadable(_logger, uri, Truncate(body), ex);

            return Error.Unavailable("ingest.source_bad_response", $"Algolia response for '{uri}' did not match the expected shape.");
        }

        if (page is null || page.Hits is null || page.NbPages < 1)
        {
            IngestLog.AlgoliaResponseEmpty(_logger, uri);

            return Error.Unavailable("ingest.source_bad_response", $"Algolia returned an empty body for '{uri}'.");
        }

        return page;
    }

    private static string Truncate(string body) =>
       body.Length > _maxLoggedBodyLength ? body[.._maxLoggedBodyLength] + "…" : body;

}
