using SearchLab.Ingest;

namespace SearchLab.UnitTests.Ingest;

public sealed class HiringThreadFilterTests
{
    [Theory]
    [InlineData("Ask HN: Who is hiring? (November 2026)")]
    [InlineData("Ask HN: Who is hiring? (March 2026)")]
    public void IsHiringThread_HiringTitle_ReturnsTrue(string title)
    {
        Assert.True(HiringThreadFilter.IsHiringThread(title));
    }

    [Theory]
    [InlineData("Ask HN: Freelancer? Seeking freelancer? (February 2026)")]
    [InlineData("Ask HN: Who wants to be hired? (September 2026)")]
    public void IsHiringThread_NonHiringTitle_ReturnsFalse(string title)
    {
        Assert.False(HiringThreadFilter.IsHiringThread(title));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsHiringThread_NullOrWhitespaceTitle_ReturnsFalse(string? title)
    {
        Assert.False(HiringThreadFilter.IsHiringThread(title));
    }
}
