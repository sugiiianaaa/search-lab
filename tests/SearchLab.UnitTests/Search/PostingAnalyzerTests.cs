using SearchLab.Search.Lucene;

namespace SearchLab.UnitTests.Search;

public sealed class PostingAnalyzerTests
{
    public sealed record Case(string Label, string Text, string[] Expected)
    {
        public override string ToString() => Label;
    }

    public static TheoryData<Case> LowercasingCases => new()
    {
        new("lowercase", "Senior ENGINEER", ["senior", "engineer"]),
    };

    public static TheoryData<Case> TechTermCases => new()
    {
        new("plus-plus-lower", "c++", ["cplusplus"]),
        new("plus-plus-upper", "C++", ["cplusplus"]),
        new("sharp", "C#", ["csharp"]),
        new("fsharp", "F#", ["fsharp"]),
        new("dotnet", ".NET", ["dotnet"]),
        new("aspnet-splits-in-two", "ASP.NET", ["aspnet", "dotnet"]),
        new("vbnet-splits-in-two", "VB.NET", ["vbnet", "dotnet"]),
        new("nodejs", "Node.js", ["nodejs"]),
    };

    public static TheoryData<Case> UnstemmedTermCases => new()
    {
        new("nodejs-keeps-its-s", "Node.js developers", ["nodejs", "developer"]),
    };

    public static TheoryData<Case> StemmingCases => new()
    {
        new("stem-plural-s", "engineers", ["engineer"]),
        new("stem-plural-ies", "companies", ["company"]),
        new("stem-noop-not-plural", "interested", ["interested"]),
    };

    public static TheoryData<Case> HtmlStrippingCases => new()
    {
        new("html-tag-stripped", "Senior Engineer<p>Apply now", ["senior", "engineer", "apply", "now"]),
        new("html-entity-stripped", "Frontend &amp; Backend", ["frontend", "backend"]),
    };

    public static TheoryData<Case> StopwordCases => new()
    {
        new("stopwords-removed", "for a senior engineer", ["senior", "engineer"]),
    };

    public static TheoryData<Case> CombinedCases => new()
    {
        new(
            "real-posting",
            "Senior Software Engineers: Frontend, Full Stack &amp; Platform<p>If you are interested",
            ["senior", "software", "engineer", "frontend", "full", "stack", "platform", "you", "interested"]),
    };

    [Theory]
    [MemberData(nameof(LowercasingCases))]
    public void Analyze_MixedCaseInput_IsLowercased(Case testCase) => AssertTokens(testCase);

    [Theory]
    [MemberData(nameof(TechTermCases))]
    public void Analyze_TechTerm_NormalizesToIndexedTerm(Case testCase) => AssertTokens(testCase);

    [Theory]
    [MemberData(nameof(UnstemmedTermCases))]
    public void Analyze_TechTerm_IsNotStemmed(Case testCase) => AssertTokens(testCase);

    [Theory]
    [MemberData(nameof(StemmingCases))]
    public void Analyze_PluralForm_IsStemmed(Case testCase) => AssertTokens(testCase);

    [Theory]
    [MemberData(nameof(HtmlStrippingCases))]
    public void Analyze_Html_IsStripped(Case testCase) => AssertTokens(testCase);

    [Theory]
    [MemberData(nameof(StopwordCases))]
    public void Analyze_Stopword_IsRemoved(Case testCase) => AssertTokens(testCase);

    [Theory]
    [MemberData(nameof(CombinedCases))]
    public void Analyze_RealPosting_ProducesExpectedTokens(Case testCase) => AssertTokens(testCase);

    private static void AssertTokens(Case testCase)
    {
        using PostingAnalyzer analyzer = new();

        Assert.Equal(testCase.Expected, analyzer.Tokenize(testCase.Text));
    }
}
