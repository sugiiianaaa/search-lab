# Conventions

How code is written in this repository. The aim: you can tell what a piece of code does from
its signature, and what it did from its output.

## Methods read as their steps

A method that does a job reads as the list of its steps. Each step is one call with a verb
for a name, and each failure returns where it happens:

```csharp
Result<string> body = await DownloadAsync(uri, cancellationToken);
if (!body.IsSuccess)
{
    return body.Error;
}

Result<AlgoliaPage<AlgoliaCommentHit>> algoliaPage = Parse(uri, body.Value);
if (!algoliaPage.IsSuccess)
{
    return algoliaPage.Error;
}

return KeepPostings(page, algoliaPage.Value);
```

The private methods follow below it, in the order they are called. Read the public method
first; it is the table of contents.

## Vocabulary

One word per concept, in code and docs alike:

- A **thread** is one "Ask HN: Who is hiring?" story.
- A **posting** is a top-level comment in a thread: one job.
- A posting is fetched as a `RawPosting`, stored in the **postings file**, and written to the
  **index**.
- A **page** is Algolia's unit of paging. It stays inside Ingest.
- A **backend** is a search engine behind `ISearchBackend`.

## Naming

- Private fields and private constants: `_camelCase` (`_httpClient`, `_hiringThreadId`).
- Everything visible outside its type: `PascalCase`.
- Booleans read as questions: `isReply`, `hasNextPage`.

The build enforces the naming rules in `.editorconfig`, so a slip fails the build.

## Project layout

```
Ingest ────────┐
               ├──> Core
Search.Lucene ─┘
Api ──> Core, Search.Lucene
Cli ──> Ingest, Search.Lucene
```

- `Core` references nothing. It holds the shared model and contracts: `RawPosting`,
  `ISearchBackend`, `SearchQuery`, `SearchOutcome`, `Result<T>`.
- Projects are separate so the compiler enforces the arrows. A reference that points
  backwards is a design problem, not an obstacle to work around.
- An interface exists only where there is more than one implementation or a caller that picks
  between them. Today that is `ISearchBackend`.
- Each class has one reason to change: `AlgoliaPostingSource` when Algolia's API changes,
  `LuceneSearchBackend` when the index or scoring changes, `SearchEndpoints` when the HTTP
  contract changes.

## Errors

- Expected failures are return values: `Result<T>` carrying an `Error`. Exceptions are for
  bugs, and bugs are not caught.
- Check `IsSuccess` and return early. `Value` and `Error` throw when read on the wrong branch.
- `Error.Code` (`search.query_empty`) is stable and is what callers branch on. Never parse
  `Error.Message`, and never put secrets or server paths in it.
- Zero results is a success, not an error.
- Cancellation is not an error: let `OperationCanceledException` propagate.

## Logging

- Log statements live in one class per area, `SearchLog` and `IngestLog`, as
  `[LoggerMessage]` methods with named placeholders, so every field is queryable.
- Event ids are grouped by level and never renumbered.

| Level | For |
|---|---|
| Information | One line per search or fetched page |
| Warning | Retries, zero results, an analyzer that produced no tokens, a query that does not parse |
| Error | Something is broken: Algolia unreachable or rejecting, the index cannot be opened |

## Comments

Write a comment only when the code cannot say it. Try a better name, a smaller method or a
named boolean first. Write a `<summary>` only when the signature leaves out something a caller
must know.

A comment earns its place when it records:

- a fact that cost time to discover and that the code cannot show, such as the trailing slash
  the Algolia base address needs;
- a trap: code that looks safe to change and is not.

No summaries that restate the name, no `<inheritdoc />`, no `<param>` tags, nothing that will go
stale. The exception is `Api/Contracts/V1`, where the XML docs become the OpenAPI schema.

## Tests

- Name tests `Method_Scenario_ExpectedResult`.
- Test the failure paths, not only the happy path.
- Prefer real objects and small hand-written fakes over mocking frameworks.
- When a test needs every case of an enum, enumerate it rather than listing the cases.
- Pure transformations such as `PostingAnalyzer` get thorough tests with real, awkward inputs.
- A test that needs a network, Docker or a clock is a sign the responsibility is in the wrong
  place.

## The development API

- The wire type is never the application type: `SearchRequest` is mapped to a validated
  `SearchQuery` in one place.
- Routes are versioned (`/v1/...`), reads are `GET`, and the response echoes the `skip` and
  `take` that were applied.
- An empty query is rejected; an oversized page is clamped.
- Every `Error` becomes an HTTP response through `ErrorMapping`.

## What we do not add

- No DDD ceremony, repositories, unit-of-work, mediators or command buses. Records with fields,
  and code that calls code.
- No abstractions over what .NET already provides, such as `TimeProvider`.
- No new abstraction without naming the concrete thing it makes testable or swappable.
- Analyzer rules are switched off only in `Directory.Build.props`, each with its reason.
