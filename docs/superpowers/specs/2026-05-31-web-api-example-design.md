# Web API Example — Design Spec

**Date:** 2026-05-31
**Sub-project:** Web API example (see [docs/ROADMAP.md](../../ROADMAP.md), "Examples / supporting work")
**Status:** Approved, ready for implementation planning.

## Goal

A small ASP.NET Core Minimal-API project under
`examples/JoakimAnder.Toolbox.Examples.WebApi/` that puts every Toolbox
feature — `Result<T, TError>`, `FanOut`, and the DI source generator — to
work together in an idiomatic shape. A reader visiting the repo to evaluate
the library sees not just isolated per-feature demos (the existing console
example covers that) but the patterns composed in a realistic service:
typed-error returns through a service layer, parallel composition for a
fan-out endpoint, attribute-driven DI registration spanning ~9 services.

## The gap

The existing console example (`JoakimAnder.Toolbox.Examples`) is excellent
for "show me what one feature does in isolation." It is not built to answer
the next question a reader asks: *what does it look like to use all three
features together in something that resembles real code I'd write?* That
question matters most when someone is deciding whether to adopt the library,
and a single program with three separate isolated demos doesn't answer it.

A purpose-built showcase fills the gap. The shape — a small HTTP service with
real CRUD-ish endpoints, a service/repo split, typed error returns and a
realistic fan-out — covers the same surface a reader would encounter in their
own code, so the patterns transfer one-for-one.

A secondary motivation surfaced during brainstorming: a *stress-test* /
performance harness for the library. That is intentionally **out of scope
here** — it belongs in a separate `tests/` benchmarks project, likely
BenchmarkDotNet-based, and gets its own brainstorm + spec when the user wants
to revisit. It is recorded in the ROADMAP's "Examples / supporting work"
section as a deferred future sub-project.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Project relationship | New project `JoakimAnder.Toolbox.Examples.WebApi` alongside the existing console example | Keep the per-feature console demos focused; the web API answers a different question |
| Web framework | ASP.NET Core Minimal APIs | Modern .NET 10 default; endpoint lambdas put the HTTP boundary directly next to the `Result.Match(...)`; lower ceremony than MVC controllers |
| Architectural shape | Vertical-slice: one folder per endpoint under `Features/<Area>/<Slice>/` | Each slice is a self-contained "feature" a reader can study independently; the pattern repetition is pedagogical |
| Domain | Books + Authors + Reviews | Familiar, relational, naturally produces a fan-out case ("book detail" = book + author + reviews) |
| Endpoint count | Six: list/get/create on books, get-by-book detail (fan-out), get author, create review | Big enough to show every `ApiError` kind in real use; small enough to stay digestible |
| Repository contract | Async-only with `CancellationToken`; `Task<T?>` for reads, `Task<bool>` for inserts | Matches I/O reality; lets handlers use natural `await` + early-return patterns and gives FanOut something cancellable to parallelize |
| Repo failure shape | Return nullable / bool — never throw, never speak `Result` | Repository abstraction stays usable from any code, not just Result-aware code; the service layer owns the Result translation |
| Service layer | Per-slice `[Scoped]` handlers — no `IXxxService` interfaces | YAGNI; the example has no automated tests, and an interface that exists only "in case" obscures the pattern. A future copy-paster needing testability adds one line |
| Error type | Caller-defined `ApiError` sealed-record DU in `Shared/Domain/` | Matches the Result spec's "boundary contract" framing; exhaustive `switch` at the HTTP mapper |
| `ApiError` cases | `NotFound(Resource, Id)`, `Validation(Field, Message)`, `Conflict(Resource, Reason)`, `Upstream(ExceptionType, Message)`, `Unexpected(Message)` | Each is structurally distinct so the HTTP mapper has the info to produce a useful response body |
| HTTP mapping | One `ApiErrorMapping.ToHttpResult(ApiError)` switch in `Shared/Endpoints/` | Cross-cutting; every endpoint calls it. Compiler enforces exhaustiveness across the DU |
| FanOut composition | `Result.TryAsync(...)` wrapping `new FanOut().Add(...).Add(...).WhenAll(...)` inside the `GetBookDetail` handler | Pattern 1 from the Result spec, used verbatim — the showcase the spec called out |
| Bind chaining showcase | Sync `Bind` pipelines for pure validation in `CreateBook` / `CreateReview` | Demonstrates short-circuit composition without needing the async surface to shine |
| Async chaining (`BindAsync` / `MapError`) | Not showcased in this example | Documented non-coverage. The service-layer-owns-its-errors shape doesn't naturally produce the case; inventing one would feel contrived. The Task 5 async extensions exist and are tested in the library |
| Persistence | In-memory dictionary repos with seeded data and small `Task.Delay(15–30ms)` per call | No external dependency; the simulated latency makes FanOut's parallelism observable (~25 ms vs ~40 ms sequential) |
| DI registration | One `builder.Services.AddAttributedServices()` line, registering 9 attributed types (3 repos `[Singleton]`, 6 handlers `[Scoped]`) | The DI generator's value proposition shown end-to-end |
| OpenAPI | `builder.Services.AddOpenApi()` + `app.MapOpenApi()` — two lines, no NuGet packages | .NET 10's built-in OpenAPI; gives the reader an importable `/openapi/v1.json` for Postman/Bruno |
| Sample requests | `WebApi.http` file at project root | Executes in VS Code REST Client, Rider, and VS 2022+ in-place; no curl knowledge needed |
| Tests | None (no test project for the example) | The example *is* documentation-as-code; the `.http` file is the smoke test. The library itself has full unit coverage |

### Why not layered (Endpoint → Service → Repo)

The classic .NET teaching shape and the runner-up. With three entities, the
layer count is small enough that there's no abstraction-for-its-own-sake
risk. Rejected on the brainstorming user's call: vertical slice better
matches "each slice is a self-contained unit a reader can study
independently." The patterns repeat visibly across slices, which helps
teaching rather than feels redundant. Trade-off accepted: ~40% more files,
each smaller.

### Why not flat Minimal API (endpoint lambdas hit repos directly)

Considered and rejected. The DI generator's value is registering *services*;
without a service layer there is nothing meaningful for `[Scoped]` to
register beyond repos (which are `[Singleton]` anyway). And the boundary
where `Result` meets HTTP collapses into the endpoint lambda, hiding the
pattern that "service returns Result, handler maps to IResult" — the single
most important thing the reader needs to see.

## Project layout

```
examples/JoakimAnder.Toolbox.Examples.WebApi/
  JoakimAnder.Toolbox.Examples.WebApi.csproj
  Program.cs
  WebApi.http
  Features/
    Books/
      ListBooks/        Endpoint.cs Handler.cs Response.cs
      GetBook/          Endpoint.cs Handler.cs Response.cs
      GetBookDetail/    Endpoint.cs Handler.cs Response.cs   ← FanOut showcase
      CreateBook/       Endpoint.cs Handler.cs Request.cs Response.cs
    Authors/
      GetAuthor/        Endpoint.cs Handler.cs Response.cs
    Reviews/
      CreateReview/     Endpoint.cs Handler.cs Request.cs Response.cs
  Shared/
    Domain/
      Book.cs Author.cs Review.cs
      ApiError.cs
    Repositories/
      BookRepository.cs    ([Singleton])
      AuthorRepository.cs  ([Singleton])
      ReviewRepository.cs  ([Singleton])
    Endpoints/
      EndpointRegistrationExtensions.cs
      ApiErrorMapping.cs
```

### csproj

`<Project Sdk="Microsoft.NET.Sdk.Web">` targeting `net10.0` (via the repo's
`Directory.Build.props`). `<OutputType>Exe</OutputType>`. Project references
to `JoakimAnder.Toolbox` and to `JoakimAnder.Toolbox.SourceGenerators` (as
`OutputItemType="Analyzer"`, identical to the existing example's csproj).
One `Microsoft.Extensions.DependencyInjection` package reference (already
centrally versioned). No other NuGets — ASP.NET Core ships as a shared
framework with the Web SDK; .NET 10's OpenAPI support is built in.

Added to `JoakimAnder.Toolbox.slnx`.

## Shared concerns

### Entities

```csharp
public sealed record Book(int Id, string Isbn, string Title, int AuthorId);
public sealed record Author(int Id, string Name);
public sealed record Review(int Id, int BookId, string Reviewer, int Rating, string Body);
```

Plain records. No behavior, no equality customization. Seeded on startup
(~5 books, ~3 authors, ~8 reviews) so a `curl` against the API returns
interesting data without a setup step.

### `ApiError` discriminated union

```csharp
public abstract record ApiError
{
    public sealed record NotFound(string Resource, string Id)              : ApiError;
    public sealed record Validation(string Field, string Message)          : ApiError;
    public sealed record Conflict(string Resource, string Reason)          : ApiError;
    public sealed record Upstream(string ExceptionType, string Message)    : ApiError;
    public sealed record Unexpected(string Message)                        : ApiError;
}
```

Every handler returns `Result<T, ApiError>`. Each case is structurally
distinct so the HTTP mapper's `switch` is exhaustive at compile time and
the wire body has the info clients need to act.

### Repositories

Async-only, cancellation-aware, never throw, never speak `Result`.

```csharp
public sealed class BookRepository
{
    Task<Book?>                       GetAsync(int id,           CancellationToken ct = default);
    Task<IReadOnlyList<Book>>         ListAsync(                 CancellationToken ct = default);
    Task<bool>                        AddAsync(Book book,        CancellationToken ct = default);   // false = duplicate ISBN
}

public sealed class AuthorRepository
{
    Task<Author?>                     GetAsync(int id,           CancellationToken ct = default);
}

public sealed class ReviewRepository
{
    Task<IReadOnlyList<Review>>       GetByBookAsync(int bookId, CancellationToken ct = default);
    Task<bool>                        AddAsync(Review review,    CancellationToken ct = default);   // false = duplicate by same reviewer
}
```

Each method honors the supplied `CancellationToken` (passes it to its
internal `Task.Delay(rand.Next(15, 30), ct)`) and throws
`OperationCanceledException` if cancelled — aligning with the `FanOut` and
`Result.TryAsync` cancellation contract.

The repo is the *only* shared dependency a slice reaches into. Repos do not
depend on each other; each owns its in-memory dictionary plus a seeded
factory method.

### `ApiErrorMapping.ToHttpResult`

```csharp
internal static class ApiErrorMapping
{
    public static IResult ToHttpResult(ApiError err) => err switch
    {
        ApiError.NotFound nf   => Results.NotFound(new { type = "NotFound",   nf.Resource, nf.Id }),
        ApiError.Validation v  => Results.BadRequest(new { type = "Validation", v.Field, v.Message }),
        ApiError.Conflict c    => Results.Conflict(new { type = "Conflict",   c.Resource, c.Reason }),
        ApiError.Upstream u    => Results.Json(new { type = "Upstream", u.ExceptionType, u.Message }, statusCode: 502),
        ApiError.Unexpected ux => Results.Problem(ux.Message, statusCode: 500),
    };
}
```

The compiler enforces exhaustiveness — adding a new `ApiError` case fails the
build until this switch is updated.

## Slice anatomy

Every slice has the same three (or four, for POST) files:

- `Response.cs` — the wire shape returned on success
- (POST only) `Request.cs` — the wire shape received in the body
- `Handler.cs` — `[Scoped]` service that does the work, returns
  `Result<T, ApiError>`; takes repos via primary constructor injection
- `Endpoint.cs` — static extension method `MapXxx(this RouteGroupBuilder)`
  that registers the route, calls the handler, `Match`es to `IResult`

Slices are independent. Each handler is concrete (no `IXxxHandler`
interface). Repos are the only thing reached into from outside the slice.

### Representative slice: `GetBook`

```csharp
// Response.cs
public sealed record BookResponse(int Id, string Isbn, string Title, int AuthorId);

// Handler.cs
[Scoped]
public sealed class GetBookHandler(BookRepository books)
{
    public async Task<Result<BookResponse, ApiError>> HandleAsync(int id, CancellationToken ct)
    {
        var book = await books.GetAsync(id, ct);
        if (book is null)
            return new ApiError.NotFound("Book", id.ToString(CultureInfo.InvariantCulture));

        return new BookResponse(book.Id, book.Isbn, book.Title, book.AuthorId);
    }
}

// Endpoint.cs
public static class GetBookEndpoint
{
    public static RouteGroupBuilder MapGetBook(this RouteGroupBuilder books)
    {
        books.MapGet("/{id:int}", async (int id, GetBookHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.Match<IResult>(
                onSuccess: response => Results.Ok(response),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("GetBook")
        .WithSummary("Get a single book by ID.");

        return books;
    }
}
```

Every other read slice (`ListBooks`, `GetAuthor`) follows this pattern.

## The FanOut showcase: `GetBookDetail`

The cross-feature composition the Result spec named "Pattern 1." A sequential
root lookup, then parallel dependency fetches wrapped with
`Result.TryAsync`, then post-processing with `TryGetValue`.

### `Response.cs`

```csharp
public sealed record BookDetailResponse(
    int Id, string Isbn, string Title,
    AuthorSummary Author,
    IReadOnlyList<ReviewSummary> Reviews);

public sealed record AuthorSummary(int Id, string Name);
public sealed record ReviewSummary(string Reviewer, int Rating, string Body);
```

### `Handler.cs`

```csharp
[Scoped]
public sealed class GetBookDetailHandler(
    BookRepository books,
    AuthorRepository authors,
    ReviewRepository reviews)
{
    public async Task<Result<BookDetailResponse, ApiError>> HandleAsync(int id, CancellationToken ct)
    {
        // 1) Book is the root — without it, every other lookup is useless.
        var book = await books.GetAsync(id, ct);
        if (book is null)
            return new ApiError.NotFound("Book", id.ToString(CultureInfo.InvariantCulture));

        // 2) Author + reviews fan out in parallel.
        //    Thrown exceptions become ApiError.Upstream via Result.TryAsync.
        //    OperationCanceledException is rethrown unchanged (per the spec's catch policy).
        var depsResult = await Result.TryAsync(
            async c =>
            {
                var (author, list) = await new FanOut()
                    .Add(t => authors.GetAsync(book.AuthorId, t))
                    .Add(t => reviews.GetByBookAsync(book.Id, t))
                    .WhenAll(c);
                return (Author: author, Reviews: list);
            },
            ex => (ApiError)new ApiError.Upstream(ex.GetType().Name, ex.Message),
            ct);

        // 3) Canonical consumer-read pattern.
        if (!depsResult.TryGetValue(out var deps, out var fetchError))
            return fetchError;

        // 4) Orphaned book (AuthorId points to a missing author).
        if (deps.Author is null)
            return new ApiError.NotFound("Author", book.AuthorId.ToString(CultureInfo.InvariantCulture));

        // 5) Compose the wire response.
        return new BookDetailResponse(
            book.Id, book.Isbn, book.Title,
            new AuthorSummary(deps.Author.Id, deps.Author.Name),
            deps.Reviews.Select(r => new ReviewSummary(r.Reviewer, r.Rating, r.Body)).ToList());
    }
}
```

What the reader sees, in order:

1. Sequential root lookup with implicit conversion on `NotFound`.
2. `Result.TryAsync` wrapping a `FanOut` — Pattern 1 verbatim.
3. `TryGetValue(out var deps, out var fetchError)` — canonical consumer read.
4. A second `NotFound` for the "orphaned book" domain case.
5. Implicit conversion to success.

The simulated repo latency (~20 ms per call) makes the parallelism
observable: the detail endpoint returns in ~25 ms despite two ~20 ms
dependencies, versus ~40 ms sequential.

## The Bind-chaining showcase: `CreateBook` and `CreateReview`

### `CreateReview`

```csharp
// Request.cs
public sealed record CreateReviewRequest(string Reviewer, int Rating, string Body);

// Response.cs
public sealed record ReviewResponse(int Id, int BookId, string Reviewer, int Rating, string Body);

// Handler.cs
[Scoped]
public sealed class CreateReviewHandler(BookRepository books, ReviewRepository reviews)
{
    public async Task<Result<ReviewResponse, ApiError>> HandleAsync(
        int bookId, CreateReviewRequest req, CancellationToken ct)
    {
        // 1) Pure validation — three sync steps chained with Bind; short-circuits on first failure.
        var validated = ValidateReviewer(req)
            .Bind(_ => ValidateRating(req))
            .Bind(_ => ValidateBody(req));

        if (!validated.TryGetValue(out _, out var validationError))
            return validationError;

        // 2) Book existence check.
        var book = await books.GetAsync(bookId, ct);
        if (book is null)
            return new ApiError.NotFound("Book", bookId.ToString(CultureInfo.InvariantCulture));

        // 3) Persist; AddAsync returns false on duplicate by same reviewer.
        var review = new Review(Id: 0, BookId: bookId, req.Reviewer, req.Rating, req.Body);
        if (!await reviews.AddAsync(review, ct))
            return new ApiError.Conflict(
                "Review", $"{req.Reviewer} has already reviewed book {bookId}.");

        return new ReviewResponse(review.Id, review.BookId, review.Reviewer, review.Rating, review.Body);
    }

    private static Result<CreateReviewRequest, ApiError> ValidateReviewer(CreateReviewRequest req) =>
        string.IsNullOrWhiteSpace(req.Reviewer)
            ? new ApiError.Validation(nameof(req.Reviewer), "Reviewer is required.")
            : req;

    private static Result<CreateReviewRequest, ApiError> ValidateRating(CreateReviewRequest req) =>
        req.Rating is < 1 or > 5
            ? new ApiError.Validation(nameof(req.Rating), "Rating must be between 1 and 5.")
            : req;

    private static Result<CreateReviewRequest, ApiError> ValidateBody(CreateReviewRequest req) =>
        string.IsNullOrWhiteSpace(req.Body)
            ? new ApiError.Validation(nameof(req.Body), "Body is required.")
            : req;
}
```

### `CreateBook`

Identical shape with a different validation set + a `NotFound` check for the
referenced `AuthorId`:

```csharp
[Scoped]
public sealed class CreateBookHandler(BookRepository books, AuthorRepository authors)
{
    public async Task<Result<BookResponse, ApiError>> HandleAsync(
        CreateBookRequest req, CancellationToken ct)
    {
        var validated = ValidateIsbn(req).Bind(_ => ValidateTitle(req));
        if (!validated.TryGetValue(out _, out var validationError))
            return validationError;

        var author = await authors.GetAsync(req.AuthorId, ct);
        if (author is null)
            return new ApiError.NotFound("Author", req.AuthorId.ToString(CultureInfo.InvariantCulture));

        var book = new Book(Id: 0, req.Isbn, req.Title, req.AuthorId);
        if (!await books.AddAsync(book, ct))
            return new ApiError.Conflict("Book", $"ISBN {req.Isbn} is already in use.");

        return new BookResponse(book.Id, book.Isbn, book.Title, book.AuthorId);
    }

    private static Result<CreateBookRequest, ApiError> ValidateIsbn(CreateBookRequest req) =>
        string.IsNullOrWhiteSpace(req.Isbn)
            ? new ApiError.Validation(nameof(req.Isbn), "ISBN is required.")
            : req;

    private static Result<CreateBookRequest, ApiError> ValidateTitle(CreateBookRequest req) =>
        string.IsNullOrWhiteSpace(req.Title)
            ? new ApiError.Validation(nameof(req.Title), "Title is required.")
            : req;
}
```

POST endpoints return `Results.Created($"/books/{response.Id}", response)`
instead of `Results.Ok(response)` — the only HTTP-layer difference from GET
endpoints. The shared `ApiErrorMapping` mapper handles failures.

## Remaining slices

- **`ListBooks`** — `Task<Result<IReadOnlyList<BookResponse>, ApiError>>`.
  Calls `books.ListAsync(ct)`, projects each entity to `BookResponse`. Happy
  path only; no failure kind applies to "list everything" without pagination.
- **`GetAuthor`** — identical structure to `GetBook`. Maps `NotFound` on miss.

The pattern repetition is intentional. A reader who saw `GetBook` knows what
`GetAuthor` looks like before opening it.

## Per-slice coverage

| Slice | `Result` | Implicit conv | `Bind` chain | `TryGetValue` | `TryAsync` | `FanOut` | `ApiError` cases |
|---|---|---|---|---|---|---|---|
| ListBooks | ✓ | ✓ | — | — | — | — | (happy path only) |
| GetBook | ✓ | ✓ | — | — | — | — | NotFound |
| **GetBookDetail** | ✓ | ✓ | — | ✓ | ✓ | ✓ | NotFound, Upstream |
| **CreateBook** | ✓ | ✓ | ✓ | ✓ | — | — | Validation, NotFound, Conflict |
| GetAuthor | ✓ | ✓ | — | — | — | — | NotFound |
| **CreateReview** | ✓ | ✓ | ✓ | ✓ | — | — | Validation, NotFound, Conflict |

Across the six slices, every Toolbox surface element appears at least once
in idiomatic context. `MapError` and `BindAsync` extensions are intentional
non-coverage: the service-layer-owns-its-errors shape does not naturally
produce those cases, and contrived demos would obscure the real patterns.
Those operators are exercised in the library's own test suite.

## Wiring

### `Program.cs`

```csharp
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// One line registers every [Singleton]/[Scoped] in the project — repos and handlers.
builder.Services.AddAttributedServices();

// .NET 10 built-in OpenAPI; reader can import /openapi/v1.json into Postman/Bruno.
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();

app
    .MapBookEndpoints()
    .MapAuthorEndpoints()
    .MapReviewEndpoints();

app.Run();
```

No middleware beyond `CreateBuilder`'s defaults. No auth, no rate limiting,
no logging configuration — the example is about the library, not about
ASP.NET Core. A real service would add those; the README says so.

### `EndpointRegistrationExtensions`

```csharp
public static class EndpointRegistrationExtensions
{
    public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/books");
        books
            .MapListBooks()
            .MapGetBook()
            .MapGetBookDetail()
            .MapCreateBook();
        return app;
    }

    public static IEndpointRouteBuilder MapAuthorEndpoints(this IEndpointRouteBuilder app)
    {
        var authors = app.MapGroup("/authors");
        authors.MapGetAuthor();
        return app;
    }

    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var reviews = app.MapGroup("/books/{bookId:int}/reviews");
        reviews.MapCreateReview();
        return app;
    }
}
```

Per-feature `MapXxx()` extensions take a `RouteGroupBuilder`. Moving a
slice's route is one edit in this file; the slice itself stays agnostic of
its mount path.

### `WebApi.http`

Sample requests at the project root; VS Code REST Client, Rider, and
VS 2022+ execute them in-place. Roughly one request per
`(slice × outcome)` pair so a reader executing top-to-bottom sees every
`ApiError` kind plus all the happy paths. The full list:

- `GET /books` — happy path, list all seeded
- `GET /books/1` — happy path, single
- `GET /books/999` — `NotFound` demo
- `GET /books/1/detail` — FanOut showcase
- `GET /authors/1` — author single
- `POST /books` with empty ISBN — `Validation`
- `POST /books` with valid payload — happy path
- `POST /books` repeated — `Conflict`
- `POST /books/1/reviews` with rating 7 — `Validation`
- `POST /books/1/reviews` valid — happy path
- `POST /books/1/reviews` repeated by same reviewer — `Conflict`

## Documentation updates

### README

Append under `## What's in the box`:

```markdown
### Web API example — `examples/JoakimAnder.Toolbox.Examples.WebApi`

A small ASP.NET Core Minimal-API project showing all three Toolbox features
composed in a realistic shape — vertical-slice handlers that return
`Result<T, ApiError>`, a `[Scoped]`/`[Singleton]` service graph wired by the
DI source generator, and a fan-out endpoint (`GET /books/{id}/detail`) that
fetches author + reviews in parallel via `Result.TryAsync(new FanOut().Add(…))`.

```sh
dotnet run --project examples/JoakimAnder.Toolbox.Examples.WebApi
```

Use `examples/JoakimAnder.Toolbox.Examples.WebApi/WebApi.http` (REST Client,
Rider, or VS 2022+ run these in-place) or the auto-generated OpenAPI
document at `/openapi/v1.json`.
```

### CHANGELOG

Under `[Unreleased]` → `### Added`:

```markdown
- `JoakimAnder.Toolbox.Examples.WebApi`: ASP.NET Core Minimal-API showcase
  exercising Result, FanOut, and the DI source generator together via
  vertical-slice handlers (~6 endpoints over Books/Authors/Reviews, in-memory
  repos with simulated latency, end-to-end `ApiError` discriminated union
  mapped to HTTP results).
```

### ROADMAP

Add a new top-level section after the existing `## Sub-projects` list:

```markdown
## Examples / supporting work

### Web API example
Minimal-API ASP.NET Core project demonstrating Result + FanOut + DI generator
together via vertical-slice handlers.

**Status:** [filled in when the implementation plan lands.]
Spec: [2026-05-31-web-api-example-design.md](superpowers/specs/2026-05-31-web-api-example-design.md).

### Performance + stress-test suite
BenchmarkDotNet harness exercising `FanOut` arity ladder, Result struct
allocation behavior, and the DI generator's incremental cache. Surfaces
library friction points (the original motivation for "stress-test the
Toolbox by using it in a non-trivial codebase").

**Status:** Not started — deferred from the Web API example brainstorm.
Likely a separate sub-project when revisited.
```

The "Done so far / Remaining" footer of the ROADMAP keeps tracking only the
library sub-projects (all complete); these examples sit in a separate
category.

## Testing

No automated tests for the example. The `.http` file is the smoke test — a
reader who can execute it sees every endpoint working end-to-end. The
library itself has full unit + integration coverage (174 tests at the time
of this spec), so the *patterns* the example demonstrates are already
proven. Adding test infrastructure to the example would conflate "showcase"
with "regression suite" and dilute both.

If the example ever drifts (a refactor breaks `GET /books/1/detail`), the
`.http` file makes it obvious in seconds. That is sufficient for a
documentation-as-code artifact.

## Scope

In scope:

- `JoakimAnder.Toolbox.Examples.WebApi` project, csproj, slnx entry.
- Six vertical slices: `ListBooks`, `GetBook`, `GetBookDetail`, `CreateBook`,
  `GetAuthor`, `CreateReview`.
- Shared entities, `ApiError` DU, in-memory async repos with simulated
  latency, the HTTP mapper, the endpoint registration composer.
- `Program.cs`, `WebApi.http`.
- Built-in OpenAPI exposure.
- README, CHANGELOG, ROADMAP updates as described.

Out of scope (YAGNI / deferred):

- Automated tests for the example (the `.http` file is the smoke test).
- A separate test project for the example.
- Auth, rate limiting, logging configuration, problem-details middleware —
  none teach Toolbox patterns.
- Persistence beyond in-memory dictionaries.
- An `IXxxHandler` interface per slice — concrete classes only.
- `MapError` / `BindAsync` demo handlers — documented non-coverage.
- Performance + stress-test harness (separate future sub-project; recorded
  in ROADMAP).

## Non-goals

- Being a production-ready service template. The example demonstrates
  library patterns; it doesn't model authentication, observability, or
  resilience.
- Exhaustive coverage of every Result / FanOut / DI-generator overload. The
  goal is *idiomatic* coverage that a reader can copy, not surface
  enumeration.
- Showcasing async `BindAsync` / `MapError` extensions. Those are tested in
  the library; the example's natural shape doesn't produce them.

## Open questions

None at design time. One implementation detail left to execution: the exact
shape of seeded data (titles, ISBNs, author names, review bodies). Any small
realistic set will do; the implementer should pick something tasteful and
deterministic (no randomization at seed time).
