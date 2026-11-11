using SearchLab.Core.Postings;
using SearchLab.Core.Results;
using SearchLab.Ingest;

namespace SearchLab.Cli;

public sealed class IngestCommand(
    AlgoliaHiringThreadSource threadSource,
    AlgoliaPostingSource postingSource,
    FileRawPostingStore store,
    PageCheckpoints checkpoints)
{
    private const int _maxThreadCount = 6;

    public async Task<int> RunAsync(int threadCount, CancellationToken cancellationToken)
    {
        if (threadCount is < 1 or > _maxThreadCount)
        {
            return Fail($"Thread count must be between 1 and {_maxThreadCount}, got {threadCount}.");
        }

        try
        {
            Result<IReadOnlyList<HiringThread>> threads = await threadSource.FetchLatestAsync(threadCount, cancellationToken);
            if (!threads.IsSuccess)
            {
                return Fail($"Could not fetch hiring threads: {threads.Error.Message}");
            }

            foreach (HiringThread thread in threads.Value)
            {
                Console.WriteLine($"{thread.Title} ({thread.Id})");

                Result<int> stored = await IngestThreadAsync(thread, cancellationToken);
                if (!stored.IsSuccess)
                {
                    return Fail($"Ingest failed for '{thread.Title}': {stored.Error.Message}");
                }

                Console.WriteLine($"  {stored.Value} new postings.");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Cancelled. Re-run to resume.");
            return 130;
        }

        return 0;
    }

    private async Task<Result<int>> IngestThreadAsync(HiringThread thread, CancellationToken cancellationToken)
    {
        int page = await checkpoints.ReadAsync(thread.Id, cancellationToken);
        int stored = 0;

        while (true)
        {
            Result<RawPostingPage> fetched = await postingSource.FetchPageAsync(page, thread.Id, cancellationToken);
            if (!fetched.IsSuccess)
            {
                return fetched.Error;
            }

            Result<int> saved = await store.SaveAsync(fetched.Value.Postings, cancellationToken);
            if (!saved.IsSuccess)
            {
                return saved.Error;
            }

            stored += saved.Value;
            Console.WriteLine($"  Page {page}: {saved.Value} new ({stored} total).");

            if (fetched.Value.IsLastPage)
            {
                // Postings arrive newest first, so the next run starts again at page 0.
                checkpoints.Clear(thread.Id);
                return stored;
            }

            // Only after the page is stored, so a killed run resumes at the same page.
            page++;
            await checkpoints.SaveAsync(thread.Id, page, cancellationToken);
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
