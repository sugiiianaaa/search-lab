using Lucene.Net.Analysis;
using Lucene.Net.Analysis.CharFilters;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.En;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Analysis.Util;

namespace SearchLab.Search.Lucene;

internal sealed class PostingAnalyzer : Analyzer
{
    private static readonly (string Surface, string Term)[] _techTerms =
    [
        ("c++",     "cplusplus"),
        ("c#",      "csharp"),
        ("f#",      "fsharp"),
        (".net",    "dotnet"),
        ("asp.net", "aspnet dotnet"),
        ("vb.net",  "vbnet dotnet"),
        ("node.js", "nodejs"),
    ];

    private static readonly NormalizeCharMap _techTermMap = BuildTechTermMap();

    private static readonly CharArraySet _unstemmedTerms = new(
        LuceneSchema.Version,
        _techTerms.SelectMany(t => t.Term.Split(' ')).Distinct().ToList(),
        ignoreCase: true);

    public List<string> Tokenize(string text)
    {
        using TokenStream tokenStream = GetTokenStream(LuceneSchema.TextField, text);
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

    // Tech terms are mapped before the tokenizer runs, because it would split "c#" and "node.js".
    protected override TextReader InitReader(string fieldName, TextReader reader)
    {
        reader = new HTMLStripCharFilter(reader);
        reader = new LowerCaseCharFilter(reader);
        reader = new MappingCharFilter(_techTermMap, reader);
        return reader;
    }

    protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
    {
        Tokenizer source = new StandardTokenizer(LuceneSchema.Version, reader);

        TokenStream result = new StandardFilter(LuceneSchema.Version, source);
        result = new SetKeywordMarkerFilter(result, _unstemmedTerms);
        result = new StopFilter(LuceneSchema.Version, result, StopAnalyzer.ENGLISH_STOP_WORDS_SET);
        result = new EnglishMinimalStemFilter(result);

        return new TokenStreamComponents(source, result);
    }

    private static NormalizeCharMap BuildTechTermMap()
    {
        NormalizeCharMap.Builder builder = new();

        foreach ((string surface, string term) in _techTerms)
        {
            builder.Add(surface, term);
        }

        return builder.Build();
    }
}
