# SearchLab

[![CI](https://github.com/sugiiianaaa/search-lab/actions/workflows/ci.yml/badge.svg)](https://github.com/sugiiianaaa/search-lab/actions/workflows/ci.yml)

A search engine I am building to learn how a search tool like Lucene actually works.

It uses Hacker News "Who is Hiring" postings as the data seed. The jobs are just the
dataset; the subject is the search system.

Making the search work is not the only goal. I want the search to be observable: how a query
is analyzed, what it gets parsed into, why a document scored what it scored, and how long
each phase took. And how the data is stored at index time and read back at search time.

It runs only on your own machine; it is never hosted.

## Try it

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). Run from the repo root:

```bash
git clone https://github.com/sugiiianaaa/search-lab.git
cd search-lab

dotnet run --project src/SearchLab.Cli -- fetch    # about 15 seconds: downloads postings into data/raw/postings.jsonl
dotnet run --project src/SearchLab.Cli -- index    # a few seconds: builds data/index/postings
dotnet run --project src/SearchLab.Cli -- search   # opens the REPL
```

Every result list comes with its trace:

```
lucene> c# remote
    1    5.35  Acme | Software Developer C#/.NET | Germany | ONSITE | …
    2    4.38  Initech | Remote (US only) | Senior SWE, Senior DevOps | …
  152 hits · :next for more
  tokens    csharp · remote
  parsed    text:csharp text:remote
  rewritten text:csharp text:remote
  phases    analyze 2.0 · parse 1.2 · score 7.3 · hydrate 4.1 · explain 7.4 ms
```

`:next` pages on, `:show 1` prints a posting in full with its HN link, `:explain 1` shows why
it scored what it scored, `:analyze <text>` shows what the analyzer makes of any text, and
`:quit` leaves. `fetch` is resumable: stop it with Ctrl+C and run it again.

## Status

Works today: fetch one "Who is hiring?" thread (September 2026, about 260 postings), build a
Lucene index from it, and search it from the terminal with the trace under every result.

Next: better analysis and query semantics, fetching many threads, pulling company, location
and salary out of the text, a full terminal UI, and Postgres full-text search as a second
backend to compare against.

## What you can see in it

- **The analyzer chain.** `C#` becomes `csharp`, `Node.js` becomes `nodejs`, `engineers`
  becomes `engineer`. A stock `StandardAnalyzer` turns both `C#` and `C++` into `c`.
- **Parsed versus rewritten query.** Try a prefix query like `engineer*` and compare the
  `parsed` and `rewritten` lines: what Lucene actually executes is not what it parsed.
- **Why a document scored what it scored.** `:explain` prints the BM25 explanation tree:
  IDF, term frequency, length normalisation.
- **Where the time went.** Per-phase timings: analyze, parse, score, hydrate, explain.
- **Deep paging.** `:next` pages with Lucene's `SearchAfter`, through a cursor stamped with
  the backend that issued it.

## Architecture

```
Ingest ────────┐
               ├──> Core
Search.Lucene ─┘
Api ──> Core, Search.Lucene
Cli ──> Ingest, Search.Lucene
```

`Core` references nothing: it holds the shared model and contracts (`RawPosting`,
`ISearchBackend`, `SearchQuery`, `SearchOutcome`, `Result<T>`). The layers are separate
projects so the compiler enforces the arrows. How code is written is in
[`docs/CONVENTIONS.md`](docs/CONVENTIONS.md).

## Development API

The same search over HTTP, for poking at it with `curl` or another tool while developing.
Local only; it has no authentication.

```bash
dotnet run --project src/SearchLab.Api
```

It listens on `http://localhost:5296` and reads the index from `Lucene:IndexPath`. In
Development that is set in `appsettings.Development.json`, relative to the API's own project
folder (which is what `dotnet run` uses as its content root), so it finds the index that
`index` built. Anywhere else, set it explicitly; the API refuses to start without it:

```bash
export Lucene__IndexPath=/absolute/path/to/data/index/postings
```

### Checking it works

Liveness:

```bash
curl http://localhost:5296/health
```

Returns `{"status":"ok"}`.

What the analyzer does to a piece of text. No index needed:

```bash
curl "http://localhost:5296/v1/analyze?text=Senior%20Rust%20Engineers%20(Remote)"
```

```json
{"backend":"lucene","input":"Senior Rust Engineers (Remote)","tokens":["senior","rust","engineer","remote"]}
```

A search. Every response carries the trace next to the hits:

```bash
curl "http://localhost:5296/v1/search?q=rust&take=1"
```

```json
{
  "hits": [{ "id": "49530430", "score": 4.5189218, "text": "..." }],
  "totalHits": 21,
  "skip": 0,
  "take": 1,
  "nextCursor": "eyJCYWNrZW5kIjoibHVjZW5lIiwiU3RhdGUiOnsiRG9jIjoxMTIsIlNjb3JlIjo0LjUxODkyMn19",
  "trace": {
    "backend": "lucene",
    "queryText": "rust",
    "analyzedTokens": ["rust"],
    "parsedQuery": "text:rust",
    "rewrittenQuery": "text:rust",
    "phases": [
      { "phase": "search.analyze", "milliseconds": 1.5757 },
      { "phase": "search.parse", "milliseconds": 0.1635 },
      { "phase": "search.score", "milliseconds": 0.4119 },
      { "phase": "search.hydrate", "milliseconds": 0.4181 },
      { "phase": "search.explain", "milliseconds": 0.1976 }
    ],
    "explanations": {
      "49530430": {
        "value": 4.5189218,
        "description": "weight(text:rust in 112) [BM25Similarity], result of:",
        "children": [
          { "value": 2.5116767, "description": "idf(docFreq=21, maxDocs=264)", "children": [] },
          { "value": 1.7991653, "description": "tfNorm, computed from:", "children": [] }
        ]
      }
    }
  }
}
```

Pass `nextCursor` back as `?cursor=` for the next page. `take` is clamped to 100; `skip` is
rejected above 1000, so anything deeper goes through the cursor.

Errors are RFC 9457 problem+json. A blank query gives a `400`:

```bash
curl -i "http://localhost:5296/v1/search"
```

```json
{
  "type": "https://searchlab.local/problems/search.query_empty",
  "title": "Invalid request",
  "status": 400,
  "detail": "Parameter 'q' is required and cannot be blank.",
  "code": "search.query_empty",
  "traceId": "00-bd9650c01cbbe5cd260b9f1be11c316e-cf055a24a68bc3f8-00"
}
```

An unknown backend gives a `404` that lists the valid names:

```bash
curl "http://localhost:5296/v1/search?q=rust&backend=nope"
```

The OpenAPI document is served in development at `http://localhost:5296/openapi/v1.json`.

## Query syntax

A query goes straight to Lucene's classic `QueryParser`, on purpose: field queries
(`id:49530430`), wildcards (`engineer*`), phrases (`"senior engineer"`), boolean
operators and even regular expressions (`/rust|go/`) all work, and the trace shows what each
one turns into. That is fine for a local learning tool. Wildcard and regex queries can get
expensive, so do not expose this API publicly without rate limiting and a narrower syntax.

## Data source

Postings are fetched at run time from the public [Hacker News Search API](https://hn.algolia.com/api),
provided by Algolia. They belong to their authors and are subject to Y Combinator's
[terms](https://www.ycombinator.com/legal/). No posting data is stored in or distributed with
this repository: `data/` is git-ignored. The license below covers the code only.

Requests are paced and identify themselves with a `SearchLab` User-Agent. SearchLab is not
affiliated with Y Combinator, Hacker News or Algolia.

## Built with

[Lucene.NET](https://lucenenet.apache.org/) (Apache License 2.0), referenced as a NuGet package
and not redistributed here.

## License

[MIT](LICENSE)
