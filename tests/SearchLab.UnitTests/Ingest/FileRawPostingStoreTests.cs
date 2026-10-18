using SearchLab.Core.Postings;
using SearchLab.Core.Results;
using SearchLab.Ingest;

namespace SearchLab.UnitTests.Ingest;

public sealed class FileRawPostingStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "searchlab-tests", Guid.NewGuid().ToString("n"));
    private readonly string _filePath;

    public FileRawPostingStoreTests()
    {
        _filePath = Path.Combine(_directory, "raw", "postings.jsonl");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_EmptyBatch_StoresNothingAndDoesNotCreateTheFile()
    {
        Result<int> result = await new FileRawPostingStore(_filePath).SaveAsync([], CancellationToken.None);

        Assert.Equal(0, StoredCount(result));
        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public async Task SaveAsync_NewPostings_WritesOneLinePerPosting()
    {
        Result<int> result = await new FileRawPostingStore(_filePath).SaveAsync([Posting(1), Posting(2), Posting(3)], CancellationToken.None);

        Assert.Equal(3, StoredCount(result));
        Assert.Equal(3, (await StoredLinesAsync()).Length);
    }

    [Fact]
    public async Task SaveAsync_MissingDirectory_CreatesIt()
    {
        await new FileRawPostingStore(_filePath).SaveAsync([Posting(1)], CancellationToken.None);

        Assert.True(File.Exists(_filePath));
    }

    [Fact]
    public async Task SaveAsync_BatchOverlappingAnEarlierBatch_StoresOnlyTheNewPostings()
    {
        FileRawPostingStore store = new(_filePath);
        await store.SaveAsync([Posting(1), Posting(2)], CancellationToken.None);

        Result<int> result = await store.SaveAsync([Posting(2), Posting(3)], CancellationToken.None);

        Assert.Equal(1, StoredCount(result));
        Assert.Equal(3, (await StoredLinesAsync()).Length);
    }

    [Fact]
    public async Task SaveAsync_SameBatchTwice_StoresNothingTheSecondTime()
    {
        FileRawPostingStore store = new(_filePath);
        RawPosting[] batch = [Posting(1), Posting(2)];

        await store.SaveAsync(batch, CancellationToken.None);
        Result<int> second = await store.SaveAsync(batch, CancellationToken.None);

        Assert.Equal(0, StoredCount(second));
        Assert.Equal(2, (await StoredLinesAsync()).Length);
    }

    [Fact]
    public async Task SaveAsync_IdRepeatedWithinOneBatch_StoresItOnce()
    {
        Result<int> result = await new FileRawPostingStore(_filePath).SaveAsync([Posting(1), Posting(1), Posting(1)], CancellationToken.None);

        Assert.Equal(1, StoredCount(result));
        Assert.Single(await StoredLinesAsync());
    }

    [Fact]
    public async Task SaveAsync_NewInstanceOverAnExistingFile_SkipsWhatIsAlreadyStored()
    {
        await new FileRawPostingStore(_filePath).SaveAsync([Posting(1), Posting(2)], CancellationToken.None);

        Result<int> result = await new FileRawPostingStore(_filePath).SaveAsync([Posting(1), Posting(2), Posting(3)], CancellationToken.None);

        Assert.Equal(1, StoredCount(result));
        Assert.Equal(3, (await StoredLinesAsync()).Length);
    }

    [Fact]
    public async Task SaveAsync_PostingContainingNewlines_StaysOnASingleLine()
    {
        RawPosting awkward = Posting(1) with { Text = "line one\nline two\r\nwith \"quotes\" and a \\ backslash" };
        FileRawPostingStore store = new(_filePath);

        await store.SaveAsync([awkward], CancellationToken.None);

        Assert.Single(await StoredLinesAsync());
        Assert.Equal(awkward.Text, (await ReadAllAsync(store)).Single().Text);
    }

    [Fact]
    public async Task SaveAsync_AfterATornFinalLine_KeepsTheNewPostingReadable()
    {
        await new FileRawPostingStore(_filePath).SaveAsync([Posting(1)], CancellationToken.None);
        await File.AppendAllTextAsync(_filePath, "{\"id\":\"c2\",\"threadI", CancellationToken.None);

        FileRawPostingStore resumed = new(_filePath);
        Result<int> result = await resumed.SaveAsync([Posting(2)], CancellationToken.None);

        Assert.Equal(1, StoredCount(result));
        Assert.Equal(["c1", "c2"], (await ReadAllAsync(resumed)).Select(posting => posting.Id));
    }

    [Fact]
    public async Task SaveAsync_PathThatCannotBeWritten_ReturnsUnavailable()
    {
        Directory.CreateDirectory(_directory);

        Result<int> result = await new FileRawPostingStore(_directory).SaveAsync([Posting(1)], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Unavailable, result.Error.Kind);
        Assert.Equal("ingest.store_unavailable", result.Error.Code);
    }

    [Fact]
    public async Task SaveAsync_CancelledToken_ThrowsInsteadOfReturningAnError()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new FileRawPostingStore(_filePath).SaveAsync([Posting(1)], cts.Token));
    }

    [Fact]
    public async Task ReadAllAsync_MissingFile_YieldsNothing()
    {
        Assert.Empty(await ReadAllAsync(new FileRawPostingStore(_filePath)));
    }

    [Fact]
    public async Task ReadAllAsync_UnparseableLine_IsSkipped()
    {
        await new FileRawPostingStore(_filePath).SaveAsync([Posting(1), Posting(2)], CancellationToken.None);
        await File.AppendAllTextAsync(_filePath, "not json at all\n", CancellationToken.None);

        Assert.Equal(2, (await ReadAllAsync(new FileRawPostingStore(_filePath))).Count);
    }

    [Fact]
    public async Task ReadAllAsync_LineMissingAProperty_IsSkipped()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(_filePath, "{\"id\":\"c1\",\"threadId\":1,\"postedAt\":\"2026-01-01T00:00:00+00:00\"}\n", CancellationToken.None);

        Assert.Empty(await ReadAllAsync(new FileRawPostingStore(_filePath)));
    }

    [Fact]
    public async Task ReadAllAsync_RoundTripsEveryField()
    {
        RawPosting original = new(
            Id: "44182203",
            ThreadId: 44159371,
            Text: "Senior Rust engineer | REMOTE | $180k-$220k",
            PostedAt: new DateTimeOffset(2026, 3, 14, 9, 26, 53, TimeSpan.FromHours(5.5)));

        await new FileRawPostingStore(_filePath).SaveAsync([original], CancellationToken.None);

        Assert.Equal(original, (await ReadAllAsync(new FileRawPostingStore(_filePath))).Single());
    }

    private async Task<string[]> StoredLinesAsync() =>
        await File.ReadAllLinesAsync(_filePath, CancellationToken.None);

    private static RawPosting Posting(int id) =>
        new($"c{id}", ThreadId: 1, $"job {id}", DateTimeOffset.UnixEpoch.AddDays(id));

    private static async Task<List<RawPosting>> ReadAllAsync(FileRawPostingStore store) =>
        await store.ReadAllAsync(CancellationToken.None).ToListAsync();

    private static int StoredCount(Result<int> result)
    {
        Assert.True(result.IsSuccess, result.IsSuccess ? null : result.Error.Message);

        return result.Value;
    }
}
