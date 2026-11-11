using System.Globalization;
using SearchLab.Cli;
using SearchLab.Core.Postings;
using SearchLab.Core.Results;
using SearchLab.Ingest;
using SearchLab.Search.Lucene;
using Microsoft.Extensions.Logging;

string postingsPath = Path.GetFullPath(Path.Combine("data", "raw", "postings.jsonl"));
string indexPath = Path.GetFullPath(Path.Combine("data", "index", "postings"));

return args.FirstOrDefault() switch
{
    "fetch" => await FetchAsync(postingsPath, CancelOnCtrlC()),
    "index" => await IndexAsync(postingsPath, indexPath, CancelOnCtrlC()),
    "search" => await SearchAsync(indexPath),
    _ => PrintUsage(),
};

static async Task<int> FetchAsync(string postingsPath, CancellationToken cancellationToken)
{
    using ILoggerFactory loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole(options => options.SingleLine = true));
    using HttpClient httpClient = CreateAlgoliaClient(loggerFactory);

    string checkpointsPath = Path.Combine(Path.GetDirectoryName(postingsPath)!, "checkpoints");

    IngestCommand ingest = new(
        new AlgoliaHiringThreadSource(httpClient, loggerFactory.CreateLogger<AlgoliaHiringThreadSource>()),
        new AlgoliaPostingSource(httpClient, loggerFactory.CreateLogger<AlgoliaPostingSource>()),
        new FileRawPostingStore(postingsPath),
        new PageCheckpoints(checkpointsPath));

    Console.WriteLine($"Postings file: {postingsPath}");

    return await ingest.RunAsync(threadCount: 6, cancellationToken);
}

static HttpClient CreateAlgoliaClient(ILoggerFactory loggerFactory)
{
    ThrottlingRetryHandler handler = new(TimeSpan.FromMilliseconds(250), maxAttempts: 4, loggerFactory.CreateLogger<ThrottlingRetryHandler>())
    {
        InnerHandler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) },
    };

    HttpClient httpClient = new(handler)
    {
        // The trailing slash matters: without it, requests go to /api/search instead of /api/v1/search.
        BaseAddress = new Uri("https://hn.algolia.com/api/v1/"),
        Timeout = TimeSpan.FromSeconds(30),
    };

    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SearchLab (+https://github.com/sugiiianaaa/search-lab)");

    return httpClient;
}

static async Task<int> IndexAsync(string postingsPath, string indexPath, CancellationToken cancellationToken)
{
    try
    {
        List<RawPosting> postings = await new FileRawPostingStore(postingsPath).ReadAllAsync(cancellationToken).ToListAsync(cancellationToken);
        Console.WriteLine($"Read {postings.Count} postings from {postingsPath}.");

        Result<int> indexed = new LuceneIndexBuilder(indexPath).Build(postings, cancellationToken);
        if (!indexed.IsSuccess)
        {
            await Console.Error.WriteLineAsync($"Index build failed: {indexed.Error.Message}");
            return 1;
        }

        Console.WriteLine($"Indexed {indexed.Value} postings into {indexPath}.");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Cancelled. Re-run to build the index from scratch.");
        return 130;
    }
}

static async Task<int> SearchAsync(string indexPath)
{
    if (!Directory.Exists(indexPath))
    {
        await Console.Error.WriteLineAsync($"No index at {indexPath}. Run fetch, then index.");
        return 1;
    }

    using ILoggerFactory loggerFactory = LoggerFactory.Create(logging => logging
        .AddSimpleConsole(options => options.SingleLine = true)
        .SetMinimumLevel(LogLevel.Error));

    LuceneSearchBackend backend = new(indexPath, loggerFactory.CreateLogger<LuceneSearchBackend>());

    await new SearchRepl(backend).RunAsync();
    return 0;
}



static CancellationToken CancelOnCtrlC()
{
    CancellationTokenSource cts = new();

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    return cts.Token;
}


static int PrintUsage()
{
    Console.WriteLine("Usage: searchlab fetch | index | search");
    Console.WriteLine("  fetch   Download postings from HN into data/raw/postings.jsonl. Resumable.");
    Console.WriteLine("  index   Build the search index in data/index/postings from the fetched postings.");
    Console.WriteLine("  search  Search the index interactively.");
    return 1;
}
