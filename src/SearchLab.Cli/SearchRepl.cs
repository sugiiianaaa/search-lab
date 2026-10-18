using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using SearchLab.Core.Results;
using SearchLab.Core.Search;

namespace SearchLab.Cli;

internal sealed partial class SearchRepl(ISearchBackend backend)
{
    private const int _pageSize = 10;
    private const int _summaryLength = 100;

    private readonly ISearchBackend _backend = backend;

    private string? _lastQuery;
    private SearchOutcome? _lastOutcome;
    private int _lastPageStart;

    public async Task RunAsync()
    {
        PrintHelp();

        while (ReadInput() is string input && input is not (":q" or ":quit"))
        {
            await HandleAsync(input);
        }
    }

    private string? ReadInput()
    {
        Console.Write($"{_backend.Name}> ");

        return Console.ReadLine()?.Trim();
    }

    private async Task HandleAsync(string input)
    {
        string[] parts = input.Split(' ', 2, StringSplitOptions.TrimEntries);
        string argument = parts.Length > 1 ? parts[1] : "";

        switch (parts[0])
        {
            case "":
                break;
            case ":help":
                PrintHelp();
                break;
            case ":next":
                await NextPageAsync();
                break;
            case ":show":
                Show(argument);
                break;
            case ":explain":
                Explain(argument);
                break;
            case ":analyze":
                await AnalyzeAsync(argument);
                break;
            default:
                await SearchAsync(new SearchQuery(input, Skip: 0, Take: _pageSize), pageStart: 0);
                break;
        }
    }

    private async Task SearchAsync(SearchQuery query, int pageStart)
    {
        Result<SearchOutcome> outcome = await _backend.SearchAsync(query, CancellationToken.None);
        if (!outcome.IsSuccess)
        {
            Console.WriteLine($"  {outcome.Error.Message}");
            return;
        }

        _lastQuery = query.Text;
        _lastOutcome = outcome.Value;
        _lastPageStart = pageStart;

        PrintHits(outcome.Value.Hits, pageStart);
        PrintTrace(outcome.Value);
    }

    private async Task NextPageAsync()
    {
        if (_lastQuery is null || _lastOutcome?.NextCursor is null)
        {
            Console.WriteLine("  No next page.");
            return;
        }

        SearchQuery next = new(_lastQuery, Skip: 0, Take: _pageSize, Cursor: _lastOutcome.NextCursor);

        await SearchAsync(next, _lastPageStart + _lastOutcome.Hits.Count);
    }

    private void Show(string argument)
    {
        if (FindHit(argument) is SearchHit hit)
        {
            Console.WriteLine($"  https://news.ycombinator.com/item?id={hit.PostingId}");
            Console.WriteLine($"  {ToPlainText(hit.Text)}");
        }
    }

    private void Explain(string argument)
    {
        if (FindHit(argument) is SearchHit hit && _lastOutcome!.Trace.Explanations.TryGetValue(hit.PostingId, out ExplanationNode? root))
        {
            PrintExplanation(root, depth: 1);
        }
    }

    private async Task AnalyzeAsync(string text)
    {
        Result<IReadOnlyList<string>> tokens = await _backend.AnalyzeAsync(text, CancellationToken.None);

        Console.WriteLine(tokens.IsSuccess ? $"  {string.Join(" · ", tokens.Value)}" : $"  {tokens.Error.Message}");
    }

    private SearchHit? FindHit(string argument)
    {
        bool isRank = int.TryParse(argument, CultureInfo.InvariantCulture, out int rank);
        int index = rank - _lastPageStart - 1;

        if (_lastOutcome is null || !isRank || index < 0 || index >= _lastOutcome.Hits.Count)
        {
            Console.WriteLine("  Give the rank of a hit on the current page, e.g. :show 1");
            return null;
        }

        return _lastOutcome.Hits[index];
    }

    private static void PrintHits(IReadOnlyList<SearchHit> hits, int pageStart)
    {
        for (int i = 0; i < hits.Count; i++)
        {
            string summary = ToPlainText(hits[i].Text);
            if (summary.Length > _summaryLength)
            {
                summary = summary[.._summaryLength] + "…";
            }

            Console.WriteLine($"  {pageStart + i + 1,3}  {hits[i].Score,6:F2}  {summary}");
        }
    }

    private static void PrintTrace(SearchOutcome outcome)
    {
        QueryTrace trace = outcome.Trace;
        string phases = string.Join(" · ", trace.Phases.Select(phase => $"{phase.Phase.Replace("search.", "")} {phase.Duration.TotalMilliseconds:F1}"));

        Console.WriteLine($"  {outcome.TotalHits} hits{(outcome.NextCursor is null ? "" : " · :next for more")}");
        Console.WriteLine($"  tokens    {string.Join(" · ", trace.AnalyzedTokens)}");
        Console.WriteLine($"  parsed    {trace.ParsedQuery}");
        Console.WriteLine($"  rewritten {trace.RewrittenQuery}");
        Console.WriteLine($"  phases    {phases} ms");
    }

    private static void PrintExplanation(ExplanationNode node, int depth)
    {
        Console.WriteLine($"{new string(' ', depth * 2)}{node.Value:F3}  {ToPlainText(node.Description)}");

        foreach (ExplanationNode child in node.Children)
        {
            PrintExplanation(child, depth + 1);
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Type a query to search. Commands:");
        Console.WriteLine("  :next            next page of the last search");
        Console.WriteLine("  :show <rank>     full text of a hit");
        Console.WriteLine("  :explain <rank>  why a hit scored what it scored");
        Console.WriteLine("  :analyze <text>  what the analyzer makes of some text");
        Console.WriteLine("  :quit");
    }

    // Postings are HTML written by strangers. Control characters are dropped so a posting
    // cannot send escape sequences to the terminal.
    private static string ToPlainText(string html)
    {
        string text = WebUtility.HtmlDecode(Tags().Replace(html, " "));
        text = new string([.. text.Select(c => char.IsControl(c) ? ' ' : c)]);

        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
