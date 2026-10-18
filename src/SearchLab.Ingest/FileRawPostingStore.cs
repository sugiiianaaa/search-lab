using System.Runtime.CompilerServices;
using System.Text.Json;
using SearchLab.Core.Postings;
using SearchLab.Core.Results;

namespace SearchLab.Ingest;

/// <summary>JSON Lines, append-only. Not thread-safe.</summary>
public sealed class FileRawPostingStore(string filePath)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
    };

    private readonly string _filePath = Path.GetFullPath(filePath);

    private HashSet<string>? _storedIds;

    /// <summary>Skips postings already stored. Returns how many were new.</summary>
    public async Task<Result<int>> SaveAsync(IReadOnlyList<RawPosting> postings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            HashSet<string> storedIds = await LoadStoredIdsAsync(cancellationToken);

            List<RawPosting> newPostings = [.. postings
                .Where(posting => !storedIds.Contains(posting.Id))
                .DistinctBy(posting => posting.Id)];

            if (newPostings.Count == 0)
            {
                return 0;
            }

            await AppendAsync(newPostings, cancellationToken);
            storedIds.UnionWith(newPostings.Select(posting => posting.Id));

            return newPostings.Count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Error.Unavailable("ingest.store_unavailable", $"Could not write to '{_filePath}': {ex.Message}");
        }
    }

    public async IAsyncEnumerable<RawPosting> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            yield break;
        }

        using StreamReader reader = new(_filePath);

        while (await reader.ReadLineAsync(cancellationToken) is string line)
        {
            if (TryDeserialize(line) is RawPosting posting)
            {
                yield return posting;
            }
        }
    }

    private async Task<HashSet<string>> LoadStoredIdsAsync(CancellationToken cancellationToken)
    {
        if (_storedIds is null)
        {
            List<RawPosting> stored = await ReadAllAsync(cancellationToken).ToListAsync(cancellationToken);
            _storedIds = [.. stored.Select(posting => posting.Id)];
        }

        return _storedIds;
    }

    private async Task AppendAsync(List<RawPosting> postings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        bool endsMidLine = EndsMidLine();

        await using StreamWriter writer = new(_filePath, append: true);

        if (endsMidLine)
        {
            await writer.WriteLineAsync();
        }

        foreach (RawPosting posting in postings)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(posting, _jsonOptions).AsMemory(), cancellationToken);
        }
    }

    // A run killed mid-write leaves a partial last line. It is skipped on read and
    // terminated before appending, so it cannot swallow the next posting.
    private bool EndsMidLine()
    {
        if (!File.Exists(_filePath))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(_filePath);

        if (stream.Length == 0)
        {
            return false;
        }

        stream.Seek(-1, SeekOrigin.End);

        return stream.ReadByte() != '\n';
    }

    private static RawPosting? TryDeserialize(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<RawPosting>(line, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
