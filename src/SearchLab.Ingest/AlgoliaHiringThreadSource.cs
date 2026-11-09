using Microsoft.Extensions.Logging;
using SearchLab.Core.Results;

namespace SearchLab.Ingest;

public sealed class AlgoliaHiringThreadSource(HttpClient httpClient, ILogger<AlgoliaHiringThreadSource> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AlgoliaHiringThreadSource> _logger = logger;

    public Task<Result<IReadOnlyList<HiringThread>>> FetchLatestAsync(int count, CancellationToken cancellationToken) =>
       throw new NotImplementedException();

}
