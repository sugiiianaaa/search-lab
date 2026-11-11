using System.Globalization;

namespace SearchLab.Cli;

/// <summary>Remembers the next page to fetch for each thread, one file per thread.</summary>
public sealed class PageCheckpoints(string directory)
{
    public async Task<int> ReadAsync(int threadId, CancellationToken cancellationToken)
    {
        string path = PathFor(threadId);
        if (!File.Exists(path))
        {
            return 0;
        }

        int page = int.Parse(await File.ReadAllTextAsync(path, cancellationToken), CultureInfo.InvariantCulture);
        Console.WriteLine($"  Resuming from page {page}.");

        return page;
    }

    public Task SaveAsync(int threadId, int page, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        return File.WriteAllTextAsync(PathFor(threadId), page.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }

    public void Clear(int threadId) => File.Delete(PathFor(threadId));

    private string PathFor(int threadId) => Path.Combine(directory, $"{threadId}.page");
}
