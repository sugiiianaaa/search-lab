using SearchLab.Core.Results;

namespace SearchLab.Core.Search;

public interface ISearchBackend
{
    string Name { get; }

    Task<Result<SearchOutcome>> SearchAsync(SearchQuery query, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<string>>> AnalyzeAsync(string text, CancellationToken cancellationToken);
}
