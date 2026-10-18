using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;

namespace SearchLab.UnitTests.Search;

// Why PostingAnalyzer exists: what Lucene's stock analyzer does to job posting text.
public sealed class StandardAnalyzerTests
{
    public static TheoryData<string, string[]> Cases => new()
    {
        { "C++", ["c"] },
        { "C#", ["c"] },
        { "Node.js", ["node.js"] },
        { "senior engineer", ["senior", "engineer"] },
        {
            "Senior Software Engineers: Frontend, Full Stack &amp; Platform<p>If you are interested",
            ["senior", "software", "engineers", "frontend", "full", "stack", "amp", "platform", "p", "you", "interested"]
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Analyze_JobPostingText_ProducesTokens(string text, string[] expected)
    {
        Assert.Equal(expected, Tokenize(text));
    }

    private static List<string> Tokenize(string text)
    {
        using Analyzer analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        using TokenStream tokenStream = analyzer.GetTokenStream("text", text);
        ICharTermAttribute term = tokenStream.AddAttribute<ICharTermAttribute>();
        List<string> tokens = [];

        tokenStream.Reset();

        while (tokenStream.IncrementToken())
        {
            tokens.Add(term.ToString());
        }

        tokenStream.End();

        return tokens;
    }
}
