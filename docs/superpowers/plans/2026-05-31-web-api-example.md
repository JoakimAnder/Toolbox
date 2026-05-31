# Web API Example Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `JoakimAnder.Toolbox.Examples.WebApi`, an ASP.NET Core Minimal-API project that exercises `Result`, `FanOut`, and the DI source generator together via vertical-slice handlers — a realistic-shape showcase a reader can `dotnet run` and `curl`.

**Architecture:** New web project alongside the existing console example. Vertical-slice layout under `Features/<Area>/<Slice>/` (Endpoint + Handler + Response, plus Request for POST). `Shared/Domain/` holds entities and the `ApiError` discriminated union; `Shared/Repositories/` holds three in-memory async repos with simulated latency; `Shared/Endpoints/` holds the `ApiError → IResult` mapper and the route-group composer. One `builder.Services.AddAttributedServices()` call wires every `[Singleton]`/`[Scoped]` type.

**Tech Stack:** C# / .NET 10, ASP.NET Core Minimal APIs (Web SDK), `Microsoft.Extensions.DependencyInjection`. No NuGets beyond those already in `Directory.Packages.props` and the shared web framework. Built-in OpenAPI (`Microsoft.AspNetCore.OpenApi`) for endpoint discovery — no Swashbuckle.

**Spec:** [docs/superpowers/specs/2026-05-31-web-api-example-design.md](../specs/2026-05-31-web-api-example-design.md)

**Plan-time refinement from spec:** Repository `AddAsync` signatures changed from `Task<bool>` to `Task<Book?>` / `Task<Review?>` (null on conflict, persisted entity with `Id` set on success). The spec's bool variant couldn't return the assigned `Id` to the handler. Behavior identical from the API contract's perspective; only the repo signature changes.

**Verification model:** The example has no automated tests (the `.http` file is the smoke test, per the spec). Each task's `Verify` is `dotnet build -c Release` succeeding cleanly; the runtime smoke is described in each task's Steps and executed manually by `dotnet run` + `curl` (or by sending requests from the `WebApi.http` file once it exists in Task 7).

---

### Task 1: Project scaffolding

**Goal:** Create the new `JoakimAnder.Toolbox.Examples.WebApi` web project, register it in the solution, and confirm it boots an empty ASP.NET Core host.

**Files:**
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/JoakimAnder.Toolbox.Examples.WebApi.csproj`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs`
- Modify: `JoakimAnder.Toolbox.slnx` (add the new project)

**Acceptance Criteria:**
- [ ] csproj uses `Microsoft.NET.Sdk.Web` and project-references the Toolbox library + source-generators (the latter as `OutputItemType="Analyzer"`), matching the existing example's csproj shape.
- [ ] `Microsoft.Extensions.DependencyInjection` is the only `PackageReference`.
- [ ] `Program.cs` builds and runs a `WebApplication` with no endpoints — process exits cleanly on Ctrl+C.
- [ ] `dotnet build -c Release` is warning-free.
- [ ] `dotnet run --project examples/JoakimAnder.Toolbox.Examples.WebApi` starts and logs `Now listening on: http://localhost:5000` (or the default kestrel port).

**Verify:** `dotnet build -c Release` → 0 errors, 0 new warnings.

**Steps:**

- [ ] **Step 1: Create the csproj**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/JoakimAnder.Toolbox.Examples.WebApi.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\..\src\JoakimAnder.Toolbox\JoakimAnder.Toolbox.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\JoakimAnder.Toolbox.SourceGenerators\JoakimAnder.Toolbox.SourceGenerators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

This mirrors `examples/JoakimAnder.Toolbox.Examples/JoakimAnder.Toolbox.Examples.csproj` exactly except for the `Sdk` value (`.Web` instead of nothing) and the absence of an `<OutputType>Exe</OutputType>` (Web SDK defaults to `Exe`).

- [ ] **Step 2: Create the minimal `Program.cs`**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "JoakimAnder.Toolbox.Examples.WebApi");
app.Run();
```

The single GET on `/` returns a name string; later tasks replace this with the real endpoint registration.

- [ ] **Step 3: Add to the solution file**

Open `JoakimAnder.Toolbox.slnx` and add a `Project` line inside the `<Folder Name="/examples/">` block (it already contains the existing examples project — model the new line on it). The exact shape is:

```xml
    <Project Path="examples/JoakimAnder.Toolbox.Examples.WebApi/JoakimAnder.Toolbox.Examples.WebApi.csproj" />
```

- [ ] **Step 4: Build cleanly**

Run: `dotnet build -c Release`
Expected: 0 errors, 0 new C# warnings (only the pre-existing NU1608 from the source-generators test project).

- [ ] **Step 5: Confirm it boots**

Run: `dotnet run --project examples/JoakimAnder.Toolbox.Examples.WebApi`
Expected: stdout includes `Now listening on: http://localhost:5000` (or another kestrel default like `5001`/`5002` depending on local config). Ctrl+C to stop.

Optionally: in another shell, `curl http://localhost:5000/` should return `JoakimAnder.Toolbox.Examples.WebApi`.

- [ ] **Step 6: Commit**

```bash
git add examples/JoakimAnder.Toolbox.Examples.WebApi/ JoakimAnder.Toolbox.slnx
git commit -m "feat(webapi-example): scaffold ASP.NET Core Minimal-API project

Creates JoakimAnder.Toolbox.Examples.WebApi with project references to
the Toolbox library and source-generators, a minimal Program.cs that
boots a WebApplication and serves a placeholder root endpoint, and the
slnx entry.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 2: Shared domain + `ApiError` DU + HTTP mapper

**Goal:** Create the entity records, the `ApiError` discriminated union, and the cross-cutting `ApiErrorMapping.ToHttpResult` switch. No behaviour yet — these are the types every slice will consume.

**Files:**
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Domain/Book.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Domain/Author.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Domain/Review.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Domain/ApiError.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/ApiErrorMapping.cs`

**Acceptance Criteria:**
- [ ] Each entity is a `public sealed record` with positional fields exactly matching the spec.
- [ ] `ApiError` is an `abstract record` with five `sealed record` nested cases: `NotFound(string Resource, string Id)`, `Validation(string Field, string Message)`, `Conflict(string Resource, string Reason)`, `Upstream(string ExceptionType, string Message)`, `Unexpected(string Message)`.
- [ ] `ApiErrorMapping.ToHttpResult(ApiError)` returns an `IResult` per case, with the body shape from the spec.
- [ ] The `switch` is exhaustive — the compiler does not emit CS8509 ("not all possible values are covered").
- [ ] `dotnet build -c Release` is warning-free.

**Verify:** `dotnet build -c Release` → 0 errors, 0 new warnings.

**Steps:**

- [ ] **Step 1: Create the entity records**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Domain/Book.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

public sealed record Book(int Id, string Isbn, string Title, int AuthorId);
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Domain/Author.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

public sealed record Author(int Id, string Name);
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Domain/Review.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

public sealed record Review(int Id, int BookId, string Reviewer, int Rating, string Body);
```

- [ ] **Step 2: Create the `ApiError` discriminated union**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Domain/ApiError.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

/// <summary>
/// Discriminated union of failure kinds returned across the API surface.
/// Every handler returns <c>Result&lt;T, ApiError&gt;</c>; the boundary
/// switch on these cases is exhaustive at compile time.
/// </summary>
public abstract record ApiError
{
    public sealed record NotFound(string Resource, string Id)              : ApiError;
    public sealed record Validation(string Field, string Message)          : ApiError;
    public sealed record Conflict(string Resource, string Reason)          : ApiError;
    public sealed record Upstream(string ExceptionType, string Message)    : ApiError;
    public sealed record Unexpected(string Message)                        : ApiError;
}
```

- [ ] **Step 3: Create the HTTP mapper**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/ApiErrorMapping.cs`:

```csharp
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;

/// <summary>
/// Cross-cutting projection from <see cref="ApiError"/> to <see cref="IResult"/>.
/// Every endpoint's failure branch calls <see cref="ToHttpResult"/>.
/// The switch is exhaustive — adding a new <c>ApiError</c> case fails the
/// build until this method is updated.
/// </summary>
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

The `Results` static class is in `Microsoft.AspNetCore.Http` (available via the Web SDK's implicit usings — no `using` directive needed for it).

- [ ] **Step 4: Build cleanly**

Run: `dotnet build -c Release`
Expected: 0 errors, 0 new warnings. If you see CS8509 (non-exhaustive `switch`), confirm all five `ApiError` cases are listed in `ApiErrorMapping.ToHttpResult`.

- [ ] **Step 5: Commit**

```bash
git add examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/
git commit -m "feat(webapi-example): add domain entities, ApiError DU, and HTTP mapper

Adds Book/Author/Review records, the ApiError sealed-record DU with
NotFound/Validation/Conflict/Upstream/Unexpected cases, and the
cross-cutting ApiErrorMapping.ToHttpResult switch every endpoint will
use to project failures into IResult responses.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 3: Repositories with seeded data and simulated latency

**Goal:** Create the three in-memory async repos. Each is `[Singleton]`-registered (so the seeded state is shared across requests), honors `CancellationToken`, and delays ~15–30 ms per call so FanOut's parallelism is observable later.

**Files:**
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Repositories/BookRepository.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Repositories/AuthorRepository.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Repositories/ReviewRepository.cs`

**Acceptance Criteria:**
- [ ] Each repo is decorated with `[Singleton]` (from `JoakimAnder.Toolbox.DependencyInjection`).
- [ ] Each method honors the supplied `CancellationToken`: passes it to its internal `Task.Delay`, so cancellation is observable.
- [ ] `BookRepository.AddAsync` returns `Task<Book?>` — null on duplicate ISBN, the persisted entity (with assigned `Id`) on success. (Spec deviation noted in plan header.)
- [ ] `ReviewRepository.AddAsync` returns `Task<Review?>` — null on duplicate `(BookId, Reviewer)`, persisted entity on success.
- [ ] `BookRepository` is seeded with 5 books, `AuthorRepository` with 3 authors, `ReviewRepository` with 8 reviews. Seeds are deterministic (no per-run randomness in the data itself).
- [ ] Simulated latency uses `Task.Delay(rand.Next(15, 30), ct)` where `rand` is a `Random(seed)` field (deterministic so timing is reproducible).
- [ ] `dotnet build -c Release` is warning-free.

**Verify:** `dotnet build -c Release` → 0 errors, 0 new warnings.

**Steps:**

- [ ] **Step 1: Create `BookRepository`**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Repositories/BookRepository.cs`:

```csharp
using System.Collections.Concurrent;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;

/// <summary>
/// In-memory book repository. Singleton — seed data persists across requests
/// for the lifetime of the host process. Each method simulates ~15–30 ms of
/// I/O latency so FanOut composition is observable.
/// </summary>
[Singleton]
public sealed class BookRepository
{
    private readonly ConcurrentDictionary<int, Book> _books = new();
    private readonly Random _latencyRandom = new(42);
    private int _nextId;

    public BookRepository()
    {
        Seed(new Book(1, "978-0-13-468599-1", "The Pragmatic Programmer", AuthorId: 1));
        Seed(new Book(2, "978-0-13-475759-9", "Refactoring",              AuthorId: 1));
        Seed(new Book(3, "978-0-321-12521-7", "Domain-Driven Design",     AuthorId: 2));
        Seed(new Book(4, "978-0-321-20068-6", "Working Effectively with Legacy Code", AuthorId: 3));
        Seed(new Book(5, "978-0-13-235088-4", "Clean Code",               AuthorId: 1));
    }

    public async Task<Book?> GetAsync(int id, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);
        return _books.TryGetValue(id, out var book) ? book : null;
    }

    public async Task<IReadOnlyList<Book>> ListAsync(CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);
        return _books.Values.OrderBy(b => b.Id).ToList();
    }

    /// <summary>
    /// Persists <paramref name="book"/> with a newly assigned Id. Returns
    /// <c>null</c> if the ISBN already exists.
    /// </summary>
    public async Task<Book?> AddAsync(Book book, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);

        // ISBN uniqueness constraint (case-insensitive).
        if (_books.Values.Any(b => string.Equals(b.Isbn, book.Isbn, StringComparison.OrdinalIgnoreCase)))
            return null;

        var id = Interlocked.Increment(ref _nextId);
        var stored = book with { Id = id };
        return _books.TryAdd(id, stored) ? stored : null;
    }

    private void Seed(Book book)
    {
        _books[book.Id] = book;
        if (book.Id > _nextId) _nextId = book.Id;
    }

    private Task SimulateLatencyAsync(CancellationToken ct)
    {
        int delay;
        lock (_latencyRandom) { delay = _latencyRandom.Next(15, 30); }
        return Task.Delay(delay, ct);
    }
}
```

- [ ] **Step 2: Create `AuthorRepository`**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Repositories/AuthorRepository.cs`:

```csharp
using System.Collections.Concurrent;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;

/// <summary>
/// In-memory author repository. Read-only (no write surface in this example).
/// </summary>
[Singleton]
public sealed class AuthorRepository
{
    private readonly ConcurrentDictionary<int, Author> _authors = new();
    private readonly Random _latencyRandom = new(7);

    public AuthorRepository()
    {
        _authors[1] = new Author(1, "Andy Hunt");
        _authors[2] = new Author(2, "Eric Evans");
        _authors[3] = new Author(3, "Michael Feathers");
    }

    public async Task<Author?> GetAsync(int id, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);
        return _authors.TryGetValue(id, out var author) ? author : null;
    }

    private Task SimulateLatencyAsync(CancellationToken ct)
    {
        int delay;
        lock (_latencyRandom) { delay = _latencyRandom.Next(15, 30); }
        return Task.Delay(delay, ct);
    }
}
```

- [ ] **Step 3: Create `ReviewRepository`**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Repositories/ReviewRepository.cs`:

```csharp
using System.Collections.Concurrent;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;

/// <summary>
/// In-memory review repository. Reviews are unique per (BookId, Reviewer).
/// </summary>
[Singleton]
public sealed class ReviewRepository
{
    private readonly ConcurrentDictionary<int, Review> _reviews = new();
    private readonly Random _latencyRandom = new(13);
    private int _nextId;

    public ReviewRepository()
    {
        Seed(new Review(1, BookId: 1, "alice",  Rating: 5, "Foundational; reread every other year."));
        Seed(new Review(2, BookId: 1, "bob",    Rating: 4, "Solid. The chapter on Coupling is gold."));
        Seed(new Review(3, BookId: 2, "alice",  Rating: 5, "The catalogue alone is worth it."));
        Seed(new Review(4, BookId: 3, "carol",  Rating: 5, "DDD finally clicked after this."));
        Seed(new Review(5, BookId: 3, "dave",   Rating: 4, "Dense in places; pace yourself."));
        Seed(new Review(6, BookId: 4, "carol",  Rating: 5, "Survival manual."));
        Seed(new Review(7, BookId: 5, "alice",  Rating: 4, "Some advice has aged better than others."));
        Seed(new Review(8, BookId: 5, "eve",    Rating: 3, "Good ideas, repetitive."));
    }

    public async Task<IReadOnlyList<Review>> GetByBookAsync(int bookId, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);
        return _reviews.Values
            .Where(r => r.BookId == bookId)
            .OrderBy(r => r.Id)
            .ToList();
    }

    /// <summary>
    /// Persists <paramref name="review"/> with a newly assigned Id. Returns
    /// <c>null</c> if (BookId, Reviewer) already has a review.
    /// </summary>
    public async Task<Review?> AddAsync(Review review, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);

        if (_reviews.Values.Any(r =>
                r.BookId == review.BookId &&
                string.Equals(r.Reviewer, review.Reviewer, StringComparison.OrdinalIgnoreCase)))
            return null;

        var id = Interlocked.Increment(ref _nextId);
        var stored = review with { Id = id };
        return _reviews.TryAdd(id, stored) ? stored : null;
    }

    private void Seed(Review review)
    {
        _reviews[review.Id] = review;
        if (review.Id > _nextId) _nextId = review.Id;
    }

    private Task SimulateLatencyAsync(CancellationToken ct)
    {
        int delay;
        lock (_latencyRandom) { delay = _latencyRandom.Next(15, 30); }
        return Task.Delay(delay, ct);
    }
}
```

- [ ] **Step 4: Build cleanly**

Run: `dotnet build -c Release`
Expected: 0 errors, 0 new warnings.

- [ ] **Step 5: Commit**

```bash
git add examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Repositories/
git commit -m "feat(webapi-example): add in-memory repositories with seeded data

Three [Singleton] repos: Book (5 seeded), Author (3 seeded), Review
(8 seeded). All methods are async, honor CancellationToken, and delay
15-30ms per call so FanOut composition is observable. AddAsync returns
the persisted entity on success (Id assigned) or null on uniqueness
constraint violation.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 4: Read slices — `ListBooks`, `GetBook`, `GetAuthor`

**Goal:** Add the three pure-read slices, the route-group composer, and wire everything through `Program.cs` so the API serves real responses end-to-end. After this task, `curl http://localhost:5000/books/1` returns the seeded book as JSON.

**Files:**
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/ListBooks/Response.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/ListBooks/Handler.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/ListBooks/Endpoint.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBook/Response.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBook/Handler.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBook/Endpoint.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Authors/GetAuthor/Response.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Authors/GetAuthor/Handler.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Authors/GetAuthor/Endpoint.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/EndpointRegistrationExtensions.cs`
- Modify: `examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs` (wire DI + the area composers)

**Acceptance Criteria:**
- [ ] Each handler is `[Scoped]`, returns `Task<Result<TResponse, ApiError>>`, and uses primary-constructor injection for its repo.
- [ ] Each endpoint extension method `MapXxx(this RouteGroupBuilder ...)` registers exactly one route on the supplied group and returns the group (so calls chain).
- [ ] `EndpointRegistrationExtensions.MapBookEndpoints(this IEndpointRouteBuilder)` creates a `/books` group and calls `MapListBooks()` + `MapGetBook()` on it (the other two book slices are added in Tasks 5 and 6).
- [ ] `EndpointRegistrationExtensions.MapAuthorEndpoints(this IEndpointRouteBuilder)` creates a `/authors` group and calls `MapGetAuthor()`.
- [ ] `Program.cs` calls `builder.Services.AddAttributedServices()` and the composer extensions.
- [ ] `curl http://localhost:5000/books` returns HTTP 200 with the seeded 5 books.
- [ ] `curl http://localhost:5000/books/1` returns HTTP 200 with `{"id":1,"isbn":"978-0-13-468599-1","title":"The Pragmatic Programmer","authorId":1}`.
- [ ] `curl http://localhost:5000/books/999` returns HTTP 404 with `{"type":"NotFound","resource":"Book","id":"999"}`.
- [ ] `curl http://localhost:5000/authors/1` returns HTTP 200 with `{"id":1,"name":"Andy Hunt"}`.
- [ ] `dotnet build -c Release` is warning-free.

**Verify:** `dotnet build -c Release` → 0 errors, 0 new warnings. Then runtime smoke: `dotnet run` in background, `curl` each endpoint as listed, terminate.

**Steps:**

- [ ] **Step 1: Create `ListBooks` slice**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/ListBooks/Response.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.ListBooks;

public sealed record BookListItem(int Id, string Isbn, string Title, int AuthorId);
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/ListBooks/Handler.cs`:

```csharp
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.ListBooks;

[Scoped]
public sealed class ListBooksHandler(BookRepository books)
{
    public async Task<Result<IReadOnlyList<BookListItem>, ApiError>> HandleAsync(CancellationToken ct)
    {
        var all = await books.ListAsync(ct);
        IReadOnlyList<BookListItem> items =
            all.Select(b => new BookListItem(b.Id, b.Isbn, b.Title, b.AuthorId)).ToList();
        return Result<IReadOnlyList<BookListItem>, ApiError>.Success(items);
    }
}
```

Note: the success branch uses the explicit `Result<…>.Success(items)` factory rather than implicit conversion. Implicit `return items;` would also work — this listing uses the explicit form because the LINQ projection's result type is `List<BookListItem>` (concrete) and reads more clearly when the wrapping is named.

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/ListBooks/Endpoint.cs`:

```csharp
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.ListBooks;

public static class ListBooksEndpoint
{
    public static RouteGroupBuilder MapListBooks(this RouteGroupBuilder books)
    {
        books.MapGet("/", async (ListBooksHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(ct);
            return result.Match<IResult>(
                onSuccess: items => Results.Ok(items),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("ListBooks")
        .WithSummary("List all books.");

        return books;
    }
}
```

- [ ] **Step 2: Create `GetBook` slice**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBook/Response.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBook;

public sealed record BookResponse(int Id, string Isbn, string Title, int AuthorId);
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBook/Handler.cs`:

```csharp
using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBook;

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
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBook/Endpoint.cs`:

```csharp
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBook;

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

- [ ] **Step 3: Create `GetAuthor` slice**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Authors/GetAuthor/Response.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Authors.GetAuthor;

public sealed record AuthorResponse(int Id, string Name);
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Authors/GetAuthor/Handler.cs`:

```csharp
using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Authors.GetAuthor;

[Scoped]
public sealed class GetAuthorHandler(AuthorRepository authors)
{
    public async Task<Result<AuthorResponse, ApiError>> HandleAsync(int id, CancellationToken ct)
    {
        var author = await authors.GetAsync(id, ct);
        if (author is null)
            return new ApiError.NotFound("Author", id.ToString(CultureInfo.InvariantCulture));

        return new AuthorResponse(author.Id, author.Name);
    }
}
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Authors/GetAuthor/Endpoint.cs`:

```csharp
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Authors.GetAuthor;

public static class GetAuthorEndpoint
{
    public static RouteGroupBuilder MapGetAuthor(this RouteGroupBuilder authors)
    {
        authors.MapGet("/{id:int}", async (int id, GetAuthorHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.Match<IResult>(
                onSuccess: response => Results.Ok(response),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("GetAuthor")
        .WithSummary("Get a single author by ID.");

        return authors;
    }
}
```

- [ ] **Step 4: Create the area composer**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/EndpointRegistrationExtensions.cs`:

```csharp
using JoakimAnder.Toolbox.Examples.WebApi.Features.Authors.GetAuthor;
using JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBook;
using JoakimAnder.Toolbox.Examples.WebApi.Features.Books.ListBooks;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;

/// <summary>
/// Composes per-feature MapXxx() extensions into per-area route-group calls.
/// Each slice owns its endpoint; this file owns the URL structure.
/// </summary>
public static class EndpointRegistrationExtensions
{
    public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/books");
        books
            .MapListBooks()
            .MapGetBook();
        // GetBookDetail and CreateBook arrive in Tasks 5 and 6.
        return app;
    }

    public static IEndpointRouteBuilder MapAuthorEndpoints(this IEndpointRouteBuilder app)
    {
        var authors = app.MapGroup("/authors");
        authors.MapGetAuthor();
        return app;
    }
}
```

- [ ] **Step 5: Wire `Program.cs`**

Replace the contents of `examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs` with:

```csharp
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// One line registers every [Singleton]/[Scoped] in the project — repos and handlers.
builder.Services.AddAttributedServices();

var app = builder.Build();

app
    .MapBookEndpoints()
    .MapAuthorEndpoints();

app.Run();
```

The placeholder `MapGet("/", ...)` from Task 1 is gone — replaced by the area composers.

- [ ] **Step 6: Build cleanly**

Run: `dotnet build -c Release`
Expected: 0 errors, 0 new warnings.

If `[Scoped]`/`[Singleton]` attributes are not found, confirm `using JoakimAnder.Toolbox.DependencyInjection;` appears in each handler and repo file. If `AddAttributedServices()` is not found, confirm the same `using` directive is present in `Program.cs`.

- [ ] **Step 7: Runtime smoke test**

In one shell: `dotnet run --project examples/JoakimAnder.Toolbox.Examples.WebApi`

In another shell, run each of the following and confirm the response:

```bash
curl -s http://localhost:5000/books | head -1
# expected: a JSON array with 5 book objects

curl -s -w "\nHTTP %{http_code}\n" http://localhost:5000/books/1
# expected: {"id":1,"isbn":"978-0-13-468599-1","title":"The Pragmatic Programmer","authorId":1}
#           HTTP 200

curl -s -w "\nHTTP %{http_code}\n" http://localhost:5000/books/999
# expected: {"type":"NotFound","resource":"Book","id":"999"}
#           HTTP 404

curl -s -w "\nHTTP %{http_code}\n" http://localhost:5000/authors/1
# expected: {"id":1,"name":"Andy Hunt"}
#           HTTP 200
```

Ctrl+C the server.

- [ ] **Step 8: Commit**

```bash
git add examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/ListBooks/ \
        examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBook/ \
        examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Authors/GetAuthor/ \
        examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/EndpointRegistrationExtensions.cs \
        examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs
git commit -m "feat(webapi-example): add ListBooks, GetBook, GetAuthor read slices

Three vertical slices following the established pattern: [Scoped]
handler returning Result<T, ApiError>, RouteGroupBuilder extension that
maps the route and projects failures via ApiErrorMapping. Composed in
EndpointRegistrationExtensions and wired through Program.cs alongside
the DI generator's AddAttributedServices() call.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 5: `GetBookDetail` slice — the FanOut showcase

**Goal:** Add the cross-feature composition slice that fans out author + reviews in parallel after the root book lookup, wrapping the FanOut call with `Result.TryAsync` so thrown exceptions become `ApiError.Upstream`. After this task, `GET /books/1/detail` returns ~25 ms despite two ~20 ms dependency calls.

**Files:**
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBookDetail/Response.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBookDetail/Handler.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBookDetail/Endpoint.cs`
- Modify: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/EndpointRegistrationExtensions.cs` (add `MapGetBookDetail()` call to the books composer)

**Acceptance Criteria:**
- [ ] `BookDetailResponse(int Id, string Isbn, string Title, AuthorSummary Author, IReadOnlyList<ReviewSummary> Reviews)` with `AuthorSummary(int Id, string Name)` and `ReviewSummary(string Reviewer, int Rating, string Body)` are declared in `Response.cs`.
- [ ] `GetBookDetailHandler` is `[Scoped]` and takes `BookRepository`, `AuthorRepository`, `ReviewRepository` via primary constructor.
- [ ] Handler returns `ApiError.NotFound("Book", id)` when the root book lookup misses.
- [ ] Handler wraps the FanOut call with `Result.TryAsync(...)` mapping any exception to `ApiError.Upstream(ex.GetType().Name, ex.Message)`.
- [ ] Handler returns `ApiError.NotFound("Author", AuthorId)` when the book references a non-existent author (orphan).
- [ ] Endpoint registered at `GET /{id:int}/detail` on the `/books` group.
- [ ] `curl http://localhost:5000/books/1/detail` returns HTTP 200 with the assembled detail (book + author + review summaries).
- [ ] `curl http://localhost:5000/books/999/detail` returns HTTP 404 NotFound for the book.
- [ ] Elapsed time for `/books/1/detail` is well under the sum of the two dependency latencies (proves parallelism). Measured manually via `curl -w "%{time_total}\n" -o /dev/null -s ...`.
- [ ] `dotnet build -c Release` is warning-free.

**Verify:** `dotnet build -c Release` → 0 errors, 0 new warnings. Then runtime smoke per Step 5 below.

**Steps:**

- [ ] **Step 1: Create the response types**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBookDetail/Response.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBookDetail;

public sealed record BookDetailResponse(
    int Id,
    string Isbn,
    string Title,
    AuthorSummary Author,
    IReadOnlyList<ReviewSummary> Reviews);

public sealed record AuthorSummary(int Id, string Name);

public sealed record ReviewSummary(string Reviewer, int Rating, string Body);
```

- [ ] **Step 2: Create the handler**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBookDetail/Handler.cs`:

```csharp
using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;
using JoakimAnder.Toolbox.Threading;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBookDetail;

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

- [ ] **Step 3: Create the endpoint**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBookDetail/Endpoint.cs`:

```csharp
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBookDetail;

public static class GetBookDetailEndpoint
{
    public static RouteGroupBuilder MapGetBookDetail(this RouteGroupBuilder books)
    {
        books.MapGet("/{id:int}/detail", async (int id, GetBookDetailHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.Match<IResult>(
                onSuccess: response => Results.Ok(response),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("GetBookDetail")
        .WithSummary("Get a book plus its author and reviews (FanOut composition).");

        return books;
    }
}
```

- [ ] **Step 4: Wire into the composer**

Open `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/EndpointRegistrationExtensions.cs`. Add the `using` directive:

```csharp
using JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBookDetail;
```

…and update `MapBookEndpoints` so the books group also calls `MapGetBookDetail()`:

```csharp
public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder app)
{
    var books = app.MapGroup("/books");
    books
        .MapListBooks()
        .MapGetBook()
        .MapGetBookDetail();
    // CreateBook arrives in Task 6.
    return app;
}
```

- [ ] **Step 5: Build + runtime smoke**

Run: `dotnet build -c Release`
Expected: 0 errors, 0 new warnings.

In one shell: `dotnet run --project examples/JoakimAnder.Toolbox.Examples.WebApi`

In another shell:

```bash
curl -s -w "\nHTTP %{http_code} time_total=%{time_total}s\n" http://localhost:5000/books/1/detail
# expected JSON: {"id":1,"isbn":"...","title":"The Pragmatic Programmer",
#                "author":{"id":1,"name":"Andy Hunt"},
#                "reviews":[{"reviewer":"alice","rating":5,"body":"..."},
#                           {"reviewer":"bob",  "rating":4,"body":"..."}]}
# expected HTTP: 200
# expected time_total: well under 0.06s (book ~25ms + parallel(author ~25ms, reviews ~25ms))

curl -s -w "\nHTTP %{http_code}\n" http://localhost:5000/books/999/detail
# expected: {"type":"NotFound","resource":"Book","id":"999"}
#           HTTP 404
```

If `time_total` for the 200 case is close to 0.075s or above, the parallelism is not happening — confirm the FanOut is correctly inside `Result.TryAsync` and the two `Add(...)` calls are not awaiting sequentially.

Ctrl+C the server.

- [ ] **Step 6: Commit**

```bash
git add examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/GetBookDetail/ \
        examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/EndpointRegistrationExtensions.cs
git commit -m "feat(webapi-example): add GetBookDetail slice with FanOut composition

The cross-feature centerpiece: a sequential root book lookup followed by
parallel author + reviews fetch via FanOut, the whole parallel call
wrapped by Result.TryAsync so any thrown exception becomes
ApiError.Upstream. Handler uses the TryGetValue early-return pattern
and a separate orphan check for AuthorId-points-to-missing-author.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 6: Write slices — `CreateBook` and `CreateReview`

**Goal:** Add the two POST endpoints that exercise `ApiError.Validation` and `ApiError.Conflict` via sync `Bind`-chained validation pipelines. After this task, every `ApiError` case is reachable from the running API.

**Files:**
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/CreateBook/Request.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/CreateBook/Response.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/CreateBook/Handler.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/CreateBook/Endpoint.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Reviews/CreateReview/Request.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Reviews/CreateReview/Response.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Reviews/CreateReview/Handler.cs`
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Reviews/CreateReview/Endpoint.cs`
- Modify: `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/EndpointRegistrationExtensions.cs` (add `MapCreateBook()` call + new `MapReviewEndpoints` method)
- Modify: `examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs` (call `MapReviewEndpoints` alongside the existing area composers)

**Acceptance Criteria:**
- [ ] `CreateBookHandler` and `CreateReviewHandler` are both `[Scoped]`, use sync `Bind` chains for validation, and follow the spec's handler shape.
- [ ] `CreateBookHandler` returns `ApiError.Validation` for empty ISBN/Title, `ApiError.NotFound` for missing `AuthorId`, `ApiError.Conflict` for duplicate ISBN, and a 201 `BookResponse` on success.
- [ ] `CreateReviewHandler` returns `ApiError.Validation` for empty Reviewer/Body or rating out of 1–5, `ApiError.NotFound` for missing book, `ApiError.Conflict` for duplicate `(BookId, Reviewer)`, and a 201 `ReviewResponse` on success.
- [ ] POST endpoints return `Results.Created("/<resource>/<id>", response)` on success (not `Results.Ok`).
- [ ] `MapReviewEndpoints` creates a `/books/{bookId:int}/reviews` group and calls `MapCreateReview()` on it.
- [ ] `Program.cs` calls `.MapReviewEndpoints()` in addition to the two existing area composers.
- [ ] `curl POST /books` with an empty ISBN returns HTTP 400 BadRequest with the Validation payload.
- [ ] `curl POST /books` with a valid body returns HTTP 201 Created with a Location header pointing at the new book.
- [ ] Repeating the same valid POST returns HTTP 409 Conflict.
- [ ] `curl POST /books/1/reviews` with rating=7 returns HTTP 400 (Validation).
- [ ] A valid POST then a duplicate (same reviewer + same book) returns 201 then 409.
- [ ] `dotnet build -c Release` is warning-free.

**Verify:** `dotnet build -c Release` → 0 errors, 0 new warnings. Then runtime smoke per Step 6 below.

**Steps:**

- [ ] **Step 1: Create the `CreateBook` slice**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/CreateBook/Request.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;

public sealed record CreateBookRequest(string Isbn, string Title, int AuthorId);
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/CreateBook/Response.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;

public sealed record BookResponse(int Id, string Isbn, string Title, int AuthorId);
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/CreateBook/Handler.cs`:

```csharp
using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;

[Scoped]
public sealed class CreateBookHandler(BookRepository books, AuthorRepository authors)
{
    public async Task<Result<BookResponse, ApiError>> HandleAsync(
        CreateBookRequest req, CancellationToken ct)
    {
        // 1) Pure validation — two steps chained with Bind, short-circuits on first failure.
        var validated = ValidateIsbn(req).Bind(_ => ValidateTitle(req));
        if (!validated.TryGetValue(out _, out var validationError))
            return validationError;

        // 2) Referenced author must exist.
        var author = await authors.GetAsync(req.AuthorId, ct);
        if (author is null)
            return new ApiError.NotFound("Author", req.AuthorId.ToString(CultureInfo.InvariantCulture));

        // 3) Persist. AddAsync returns null on duplicate ISBN.
        var stored = await books.AddAsync(
            new Book(Id: 0, req.Isbn, req.Title, req.AuthorId), ct);
        if (stored is null)
            return new ApiError.Conflict("Book", $"ISBN {req.Isbn} is already in use.");

        return new BookResponse(stored.Id, stored.Isbn, stored.Title, stored.AuthorId);
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

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/CreateBook/Endpoint.cs`:

```csharp
using System.Globalization;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;

public static class CreateBookEndpoint
{
    public static RouteGroupBuilder MapCreateBook(this RouteGroupBuilder books)
    {
        books.MapPost("/", async (CreateBookRequest req, CreateBookHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(req, ct);
            return result.Match<IResult>(
                onSuccess: created =>
                    Results.Created($"/books/{created.Id.ToString(CultureInfo.InvariantCulture)}", created),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("CreateBook")
        .WithSummary("Create a book.");

        return books;
    }
}
```

- [ ] **Step 2: Create the `CreateReview` slice**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Reviews/CreateReview/Request.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;

public sealed record CreateReviewRequest(string Reviewer, int Rating, string Body);
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Reviews/CreateReview/Response.cs`:

```csharp
namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;

public sealed record ReviewResponse(int Id, int BookId, string Reviewer, int Rating, string Body);
```

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Reviews/CreateReview/Handler.cs`:

```csharp
using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;

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

        // 3) Persist; AddAsync returns null on (BookId, Reviewer) duplicate.
        var stored = await reviews.AddAsync(
            new Review(Id: 0, BookId: bookId, req.Reviewer, req.Rating, req.Body), ct);
        if (stored is null)
            return new ApiError.Conflict(
                "Review", $"{req.Reviewer} has already reviewed book {bookId.ToString(CultureInfo.InvariantCulture)}.");

        return new ReviewResponse(stored.Id, stored.BookId, stored.Reviewer, stored.Rating, stored.Body);
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

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Reviews/CreateReview/Endpoint.cs`:

```csharp
using System.Globalization;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;

public static class CreateReviewEndpoint
{
    public static RouteGroupBuilder MapCreateReview(this RouteGroupBuilder reviews)
    {
        reviews.MapPost("/", async (int bookId, CreateReviewRequest req, CreateReviewHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(bookId, req, ct);
            return result.Match<IResult>(
                onSuccess: created =>
                    Results.Created(
                        $"/books/{bookId.ToString(CultureInfo.InvariantCulture)}/reviews/{created.Id.ToString(CultureInfo.InvariantCulture)}",
                        created),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("CreateReview")
        .WithSummary("Create a review for a book.");

        return reviews;
    }
}
```

`bookId` is bound from the route template `/books/{bookId:int}/reviews` defined in the composer (next step).

- [ ] **Step 3: Wire into the composer**

Open `examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/EndpointRegistrationExtensions.cs`. Add the two new `using` directives:

```csharp
using JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;
using JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;
```

Update `MapBookEndpoints` so the books group also calls `MapCreateBook()`:

```csharp
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
```

Add a new `MapReviewEndpoints` method to the class:

```csharp
public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
{
    var reviews = app.MapGroup("/books/{bookId:int}/reviews");
    reviews.MapCreateReview();
    return app;
}
```

- [ ] **Step 4: Wire `Program.cs`**

Replace the body of `Program.cs` so the three composers are chained:

```csharp
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAttributedServices();

var app = builder.Build();

app
    .MapBookEndpoints()
    .MapAuthorEndpoints()
    .MapReviewEndpoints();

app.Run();
```

- [ ] **Step 5: Build cleanly**

Run: `dotnet build -c Release`
Expected: 0 errors, 0 new warnings.

- [ ] **Step 6: Runtime smoke test**

In one shell: `dotnet run --project examples/JoakimAnder.Toolbox.Examples.WebApi`

In another shell:

```bash
# Validation: empty ISBN -> 400
curl -s -w "\nHTTP %{http_code}\n" -X POST http://localhost:5000/books \
  -H "Content-Type: application/json" \
  -d '{"isbn":"","title":"A New Hope","authorId":1}'
# expected: {"type":"Validation","field":"Isbn","message":"ISBN is required."}
#           HTTP 400

# Happy path -> 201 with Location
curl -s -i -X POST http://localhost:5000/books \
  -H "Content-Type: application/json" \
  -d '{"isbn":"978-0-13-117705-7","title":"Effective C#","authorId":1}'
# expected: HTTP/1.1 201 Created
#           Location: /books/6
#           {"id":6,"isbn":"978-0-13-117705-7","title":"Effective C#","authorId":1}

# Duplicate ISBN -> 409
curl -s -w "\nHTTP %{http_code}\n" -X POST http://localhost:5000/books \
  -H "Content-Type: application/json" \
  -d '{"isbn":"978-0-13-117705-7","title":"Effective C# 2nd Edition","authorId":1}'
# expected: {"type":"Conflict","resource":"Book","reason":"ISBN 978-0-13-117705-7 is already in use."}
#           HTTP 409

# Author not found -> 404
curl -s -w "\nHTTP %{http_code}\n" -X POST http://localhost:5000/books \
  -H "Content-Type: application/json" \
  -d '{"isbn":"978-1-11-122233-3","title":"Mystery Book","authorId":999}'
# expected: {"type":"NotFound","resource":"Author","id":"999"}
#           HTTP 404

# CreateReview validation: rating out of range -> 400
curl -s -w "\nHTTP %{http_code}\n" -X POST http://localhost:5000/books/1/reviews \
  -H "Content-Type: application/json" \
  -d '{"reviewer":"frank","rating":7,"body":"Great."}'
# expected: {"type":"Validation","field":"Rating","message":"Rating must be between 1 and 5."}
#           HTTP 400

# CreateReview happy path -> 201
curl -s -i -X POST http://localhost:5000/books/1/reviews \
  -H "Content-Type: application/json" \
  -d '{"reviewer":"frank","rating":5,"body":"Loved it."}'
# expected: HTTP/1.1 201 Created
#           Location: /books/1/reviews/9
#           {"id":9,"bookId":1,"reviewer":"frank","rating":5,"body":"Loved it."}

# CreateReview conflict (frank again on book 1) -> 409
curl -s -w "\nHTTP %{http_code}\n" -X POST http://localhost:5000/books/1/reviews \
  -H "Content-Type: application/json" \
  -d '{"reviewer":"frank","rating":3,"body":"On second thought..."}'
# expected: {"type":"Conflict","resource":"Review","reason":"frank has already reviewed book 1."}
#           HTTP 409

# CreateReview NotFound (book 999 doesn't exist) -> 404
curl -s -w "\nHTTP %{http_code}\n" -X POST http://localhost:5000/books/999/reviews \
  -H "Content-Type: application/json" \
  -d '{"reviewer":"frank","rating":5,"body":"Good."}'
# expected: {"type":"NotFound","resource":"Book","id":"999"}
#           HTTP 404
```

Ctrl+C the server.

- [ ] **Step 7: Commit**

```bash
git add examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Books/CreateBook/ \
        examples/JoakimAnder.Toolbox.Examples.WebApi/Features/Reviews/ \
        examples/JoakimAnder.Toolbox.Examples.WebApi/Shared/Endpoints/EndpointRegistrationExtensions.cs \
        examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs
git commit -m "feat(webapi-example): add CreateBook and CreateReview write slices

Two POST slices showcasing sync Bind-chained validation pipelines:
CreateBook (Validation/NotFound/Conflict) and CreateReview (three
validation steps chained with Bind, plus NotFound/Conflict). Each
returns 201 Created with a Location header on success. Composed via
EndpointRegistrationExtensions; MapReviewEndpoints is the new third
area composer.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 7: OpenAPI exposure + `WebApi.http` + final smoke

**Goal:** Enable .NET 10's built-in OpenAPI so the example exposes a generated document at `/openapi/v1.json`, add the project-root `WebApi.http` file with one request per `(slice × outcome)` pair, and run a final end-to-end smoke covering every endpoint.

**Files:**
- Modify: `examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs` (add the two OpenAPI lines)
- Create: `examples/JoakimAnder.Toolbox.Examples.WebApi/WebApi.http`

**Acceptance Criteria:**
- [ ] `Program.cs` calls `builder.Services.AddOpenApi()` before `Build()` and `app.MapOpenApi()` after.
- [ ] `curl http://localhost:5000/openapi/v1.json` returns HTTP 200 with a JSON document describing the six endpoints.
- [ ] `WebApi.http` exists at the project root with at least one request per `(slice × outcome)` pair from the spec's "WebApi.http" section.
- [ ] All requests in `WebApi.http` execute successfully (manually verified by running them via VS Code REST Client / Rider / VS 2022 or by sending the equivalent `curl` commands).
- [ ] `dotnet build -c Release` is warning-free.

**Verify:** `dotnet build -c Release` → 0 errors, 0 new warnings. Then runtime smoke per Step 3 below.

**Steps:**

- [ ] **Step 1: Add OpenAPI to `Program.cs`**

Open `examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs` and update to:

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

`AddOpenApi()` and `MapOpenApi()` come from `Microsoft.AspNetCore.OpenApi`, which ships as part of the shared web framework — no NuGet package reference is needed.

- [ ] **Step 2: Create `WebApi.http`**

Create `examples/JoakimAnder.Toolbox.Examples.WebApi/WebApi.http`:

```http
@host = http://localhost:5000

### Happy path — list all seeded books
GET {{host}}/books

### Get a specific book
GET {{host}}/books/1

### NotFound demo
GET {{host}}/books/999

### FanOut showcase — book + author + reviews in parallel
GET {{host}}/books/1/detail

### Get an author
GET {{host}}/authors/1

### Author NotFound
GET {{host}}/authors/999

### CreateBook — validation failure (empty Isbn)
POST {{host}}/books
Content-Type: application/json

{ "isbn": "", "title": "A New Hope", "authorId": 1 }

### CreateBook — happy path
POST {{host}}/books
Content-Type: application/json

{ "isbn": "978-0-13-117705-7", "title": "Effective C#", "authorId": 1 }

### CreateBook — conflict (re-run the previous request once it has succeeded)
POST {{host}}/books
Content-Type: application/json

{ "isbn": "978-0-13-117705-7", "title": "Effective C# 2nd Edition", "authorId": 1 }

### CreateBook — author NotFound
POST {{host}}/books
Content-Type: application/json

{ "isbn": "978-1-11-122233-3", "title": "Mystery Book", "authorId": 999 }

### CreateReview — validation failure (rating out of range)
POST {{host}}/books/1/reviews
Content-Type: application/json

{ "reviewer": "frank", "rating": 7, "body": "Great." }

### CreateReview — happy path
POST {{host}}/books/1/reviews
Content-Type: application/json

{ "reviewer": "frank", "rating": 5, "body": "Loved it." }

### CreateReview — conflict (frank tries to review book 1 again)
POST {{host}}/books/1/reviews
Content-Type: application/json

{ "reviewer": "frank", "rating": 4, "body": "On second thought..." }

### CreateReview — NotFound (book 999 doesn't exist)
POST {{host}}/books/999/reviews
Content-Type: application/json

{ "reviewer": "frank", "rating": 5, "body": "Good." }

### OpenAPI document
GET {{host}}/openapi/v1.json
```

The `@host` variable at the top makes it trivial to point the requests at a different base URL (e.g., a deployed instance).

- [ ] **Step 3: Build + final smoke**

Run: `dotnet build -c Release`
Expected: 0 errors, 0 new warnings.

In one shell: `dotnet run --project examples/JoakimAnder.Toolbox.Examples.WebApi`

In another shell, run a quick coverage pass:

```bash
# OpenAPI document
curl -s -w "\nHTTP %{http_code}\n" -o /dev/null http://localhost:5000/openapi/v1.json
# expected: HTTP 200

# Sanity check that OpenAPI lists the endpoints
curl -s http://localhost:5000/openapi/v1.json | grep -o '"/books[^"]*"' | sort -u
# expected output includes: "/books", "/books/{id}", "/books/{id}/detail", "/books/{bookId}/reviews"
```

Then either open `WebApi.http` in VS Code (with the REST Client extension) / Rider / VS 2022 and execute each request top-to-bottom, OR translate each `###` block to a `curl` and exercise them manually. Every request should return the expected status and JSON body.

Ctrl+C the server.

- [ ] **Step 4: Commit**

```bash
git add examples/JoakimAnder.Toolbox.Examples.WebApi/Program.cs \
        examples/JoakimAnder.Toolbox.Examples.WebApi/WebApi.http
git commit -m "feat(webapi-example): expose OpenAPI document and add WebApi.http

Two-line hook into .NET 10's built-in OpenAPI exposes a generated
endpoint document at /openapi/v1.json (importable into Postman/Bruno).
Project-root WebApi.http file holds one request per (slice x outcome)
pair so a reader can exercise every endpoint and ApiError case in
sequence via VS Code REST Client, Rider, or VS 2022+ without writing
curl commands.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 8: README, CHANGELOG, and ROADMAP updates

**Goal:** Document the new example in `README.md`, record the addition in `CHANGELOG.md`, and add the new `Examples / supporting work` section to `docs/ROADMAP.md` with entries for both the Web API example (Complete) and the deferred Performance + stress-test suite (Not started).

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/ROADMAP.md`

**Acceptance Criteria:**
- [ ] README's `## What's in the box` gets a new `### Web API example` subsection at the end (after the DI registration subsection).
- [ ] CHANGELOG `[Unreleased]` → `### Added` gains the Web API example entry.
- [ ] ROADMAP grows a new `## Examples / supporting work` section after `## Sub-projects`, containing two entries: Web API example (Complete) and Performance + stress-test suite (Not started).
- [ ] ROADMAP's `## Order` and "Done so far / Remaining" footer is left unchanged (those still reflect *library* sub-projects, not example/support work).
- [ ] `dotnet build -c Release` still passes; full library test suite still green.

**Verify:** `dotnet build -c Release` → 0 errors, 0 new warnings. `dotnet test` → all pass. Plus a manual `grep` of the three doc files confirming the new content lands as expected.

**Steps:**

- [ ] **Step 1: Update `README.md`**

In `README.md`, locate the existing DI registration subsection (starts with `### DI registration — attributes + source generator`) and identify where it ends — the next line is `## Project structure`. Insert the following new subsection between the end of the DI registration section and the `## Project structure` heading:

````markdown
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
Rider, or VS 2022+ run these in-place) or the auto-generated OpenAPI document
at `/openapi/v1.json`.
````

The line `See [docs/ROADMAP.md](docs/ROADMAP.md) for the project roadmap.` stays where it is.

- [ ] **Step 2: Update `CHANGELOG.md`**

In `CHANGELOG.md`, under `## [Unreleased]` → `### Added`, append this new bullet point at the end of the list (after the existing Result entry):

```markdown
- `JoakimAnder.Toolbox.Examples.WebApi`: ASP.NET Core Minimal-API showcase exercising Result, FanOut, and the DI source generator together via vertical-slice handlers (~6 endpoints over Books/Authors/Reviews, in-memory repos with simulated latency, end-to-end `ApiError` discriminated union mapped to HTTP results).
```

- [ ] **Step 3: Update `docs/ROADMAP.md`**

In `docs/ROADMAP.md`, locate the existing `## Order` heading (or whatever heading directly follows the last sub-project block — currently the DI Attributes + Source Generator entry). Insert this new top-level section **before** `## Order`:

```markdown
## Examples / supporting work

These artifacts support adoption and library quality. They live alongside the
library code but are not library sub-projects, so they are tracked separately
from the "Done so far / Remaining" footer below.

### Web API example
Minimal-API ASP.NET Core project demonstrating Result + FanOut + DI generator
together via vertical-slice handlers.

**Status:** Complete. Spec: [2026-05-31-web-api-example-design.md](superpowers/specs/2026-05-31-web-api-example-design.md), plan: [2026-05-31-web-api-example.md](superpowers/plans/2026-05-31-web-api-example.md).

### Performance + stress-test suite
BenchmarkDotNet harness exercising `FanOut` arity ladder, Result struct
allocation behavior, and the DI generator's incremental cache. Surfaces
library friction points (the original motivation for "stress-test the
Toolbox by using it in a non-trivial codebase").

**Status:** Not started — deferred from the Web API example brainstorm.
Likely a separate sub-project when revisited.

```

Leave the `## Order` section, the `Foundation → CI/CD → features` line, and the `**Done so far:**` footer unchanged. The footer is exclusively about library sub-projects.

- [ ] **Step 4: Verify everything still builds, runs, and passes**

Run: `dotnet build -c Release`
Expected: 0 errors, 0 new warnings.

Run: `dotnet test`
Expected: all library tests still pass (no change in count — this task didn't touch any library code).

Run the example one last time to confirm nothing regressed:
```bash
dotnet run --project examples/JoakimAnder.Toolbox.Examples.WebApi
# In another shell:
curl -s -w "\nHTTP %{http_code}\n" http://localhost:5000/books/1/detail
# expected: 200 with the assembled detail
```

Ctrl+C the server.

- [ ] **Step 5: Commit (two commits — feature docs, then roadmap)**

Mirror the existing precedent of separating feature documentation from roadmap status changes:

```bash
git add README.md CHANGELOG.md
git commit -m "docs(webapi-example): document Web API example in README and CHANGELOG

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"

git add docs/ROADMAP.md
git commit -m "docs: add Examples / supporting work section to roadmap

Records the Web API example as Complete and the future Performance +
stress-test suite as Not started (deferred). The library sub-project
list and 'Done so far / Remaining' footer are unchanged.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Notes for the implementer

- **No automated tests for the example.** This is intentional (spec decision). The `.http` file is the smoke test; every meaningful behavior is observable via a request. The library itself has full test coverage (174 tests) so the patterns demonstrated are already proven.

- **Background server runs.** Tasks 4–8 each require running the server (`dotnet run --project examples/JoakimAnder.Toolbox.Examples.WebApi`) and exercising endpoints from a second shell. On platforms where backgrounding is awkward, run the server in one terminal, do the smoke in another, then Ctrl+C the server before moving on.

- **Default ports.** ASP.NET Core's default port is `http://localhost:5000` (and `https://localhost:5001` if HTTPS profile is configured). If a local config puts the example on a different port, adjust the `curl` URLs and `WebApi.http`'s `@host` variable.

- **`AddAttributedServices()` covers every attributed type in the project.** Adding a new `[Scoped]` handler in a future slice requires no changes to `Program.cs` — the DI generator picks it up at build. This is the value proposition the example is showcasing.

- **`ApiError` case additions.** If a future change adds a new `ApiError` case (e.g., `RateLimited`), the compiler refuses to build until `ApiErrorMapping.ToHttpResult`'s `switch` covers it — by design.

- **`StringComparison.OrdinalIgnoreCase`** is used for ISBN and reviewer-name uniqueness so case differences don't sneak in. If a future change normalizes input at the boundary, this can become `Ordinal`.

- **Zero warnings is a gate.** Same as the library: `AnalysisMode=Recommended`, `EnforceCodeStyleInBuild=true`. Watch for CA1305 (culture-aware `ToString`), CA1715 (generic type-parameter naming — won't apply here since no new generic types are introduced), and IDE0011 (brace requirements per `.editorconfig`).

- **`return value;` works because of the implicit conversions** from `T` and `TError` on `Result<T, TError>`. The handlers rely on this heavily; it is the whole point of the Result spec's producer-side ergonomics.

- **The `[Scoped]` lifetime matches an HTTP request.** Each request gets a fresh handler instance; repos (`[Singleton]`) hold seeded state across requests. Don't change those lifetimes without considering shared-state implications.
