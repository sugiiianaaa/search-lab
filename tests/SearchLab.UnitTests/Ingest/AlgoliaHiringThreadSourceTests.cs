using System.Net;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using SearchLab.Core.Results;
using SearchLab.Ingest;
using SearchLab.UnitTests.Support;

namespace SearchLab.UnitTests.Ingest;

public sealed class AlgoliaHiringThreadSourceTests
{
    // Discovery should only keep the hiring thread.
    [Fact]
    public async Task FetchLatestAsync_MixedStories_KeepsOnlyHiringThreadsFromWhoIsHiring()
    {
        string body = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Ingest", "Fixtures", "algolia_who_is_hiring_thread.json"));

        StubHttpHandler handler = new(HttpStatusCode.OK, body);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://hn.algolia.com/api/v1/") };
        AlgoliaHiringThreadSource source = new(httpClient, NullLogger<AlgoliaHiringThreadSource>.Instance);

        Result<IReadOnlyList<HiringThread>> result = await source.FetchLatestAsync(count: 12, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [18807017, 15824597, 15601729],
            result.Value.Select(thread => thread.Id));
    }

    // Discovery should only fetch the latest x threads.
    [Fact]
    public async Task FetchLatestAsync_TooManyThreads_OnlyReturnsLatestXThreads()
    {
        string body = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Ingest", "Fixtures", "algolia_who_is_hiring_thread.json"));


        StubHttpHandler handler = new(HttpStatusCode.OK, body);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://hn.algolia.com/api/v1/") };
        AlgoliaHiringThreadSource source = new(httpClient, NullLogger<AlgoliaHiringThreadSource>.Instance);

        Result<IReadOnlyList<HiringThread>> result = await source.FetchLatestAsync(count: 2, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([18807017, 15824597], result.Value.Select(thread => thread.Id));
    }

    // Bad parsing should not throw an exception, but instead return results error.
    [Fact]
    public async Task FetchLatestAsync_MissingHits_ReturnsErrorResult()
    {
        StubHttpHandler handler = new(HttpStatusCode.OK, "{ \"hits\": [] }");
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://hn.algolia.com/api/v1/") };
        AlgoliaHiringThreadSource source = new(httpClient, NullLogger<AlgoliaHiringThreadSource>.Instance);

        Result<IReadOnlyList<HiringThread>> result = await source.FetchLatestAsync(count: 12, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ingest.source_bad_response", result.Error.Code);
    }
}
