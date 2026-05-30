# Result — Design Spec

**Date:** 2026-05-30
**Sub-project:** Result class (see [docs/ROADMAP.md](../../ROADMAP.md))
**Status:** Approved, ready for implementation planning.

## Goal

A typed success/failure container for the **boundaries** of code — the seams
where a unit of work either completes producing a value or fails with a known
kind of error. The container's job is to make that fork **legible in the
signature** so callers cannot accidentally treat a failure as a success, and to
compose across `await` so async pipelines stay honest end-to-end.

## The gap

The BCL has no shared shape for "succeeded with a value or failed with a typed
error." The available conventions all leak:

- `throw` for expected failures pulls them out of the signature and into
  documentation. Callers cannot tell from a method's type which kinds of
  failure are reachable, and the compiler cannot enforce that the boundary
  handles them.
- `bool TryDo(out T value)` reduces the failure to a single bit. There is
  nowhere to carry the *kind* of failure that handlers need to branch on (a
  controller mapping `NotFound` to 404 and `Validation` to 400 cannot be
  written against `bool`).
- `(bool ok, T value, string error)` tuples push the discipline back onto every
  call site — there is no compile-time guarantee that the caller checks `ok`
  before reading `value`.

`Result<T, TError>` fills the gap: a value-typed tagged union that the type
system enforces at every read, with composition operators that survive `await`.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Primary types | `Result<TError>` (void success) + `Result<T, TError>` (typed success) | Mirrors `Task` / `Task<T>`; same ladder convention as `FanOut` |
| Error representation | Caller's `TError` — no built-in `Error` type | Project domain owns the error taxonomy; the library does not dictate vocabulary |
| `TError` constraint | `where TError : notnull` | Avoids `Result<T, TError?>` ambiguity; failures must carry something |
| Type kind | `readonly struct` with private `byte _state` discriminator | Zero heap allocation per Result on hot paths; `default` is a known-bad state that throws; consistent with `FanOut` |
| Default value | `default(Result<…>)` → `_state == 0` → operations throw `InvalidOperationException` | Same precedent as `ImmutableArray<T>.IsDefault`; loud failure beats silent ghost-success |
| Producer ergonomics | Implicit conversions from `T` and `TError`, plus `Result<T,E>.Success` / `Result<T,E>.Failure` factories as fallback | Best return-site ergonomics; factories cover the `T`-equals-`TError` ambiguity |
| Consumer ergonomics | `Match` (returning + void overloads) + `TryGetValue(out T, out TError)` / `TryGetError(out TError)` | Both exhaustive by construction; no `Value` / `Error` properties to misuse |
| Operators | `Map`, `MapError`, `Bind` — sync and async — on both `Result` and `Task<Result>` | Compose Result-returning calls across `await` boundaries cleanly |
| Exception interop | `Result.Try` / `TryAsync` (in) + `ValueOrThrow` / `ThrowIfFailure` (out), with async siblings | Boundaries cross between throwing and Result worlds; the helpers are the toolkit |
| `Try` catch policy | Catch every `Exception` except `OperationCanceledException` (rethrown) | Cancellation must stay cancellation; everything else is a domain failure the caller mapped |
| Equality | None opted-in (the struct's default field-wise equality only) | `TError`'s equality is unknown; the library should not make a brittle commitment |
| Namespace | `JoakimAnder.Toolbox.Results` | Groups Result-related types; sibling to `Threading` |
| FanOut composition | In scope: docs + examples for "wrap with `Result.TryAsync`" (pattern 1) and "ops return `Result`, collapse with `Bind`" (pattern 2). Out of scope: a Result-aware `FanOut` overload | Pattern 1 needs zero new code; pattern 2 demonstrates `Bind`'s value; pattern 3 is its own sub-project |

### Why not a built-in `Error` type

Considered and rejected. The strongest available shape — a `record struct`
with `string Code` and `string Message` plus curated factories
(`Error.NotFound(...)`, `Error.Validation(...)`, etc.) — has two problems:

1. **Stringly-typed `Code` defeats exhaustive branching at the boundary.** A
   handler `switch`ing on `err.Code` has no compile-time guarantee that every
   producer's code is covered, and adding a new error kind silently bypasses
   every existing `switch`.
2. **Baking a vocabulary into the library forces every domain to adopt the
   same taxonomy.** Real applications already model their domain errors
   (often as sealed-record discriminated unions), and the library's `Error`
   would shadow or duplicate that work.

Callers bring their own `TError` — typically a sealed-record DU per bounded
context — and the boundary `switch` is exhaustive at compile time.

### Why not `record struct`

`readonly record struct` would supply value equality and `with`-expressions for
free, but:

- `with` is useless for a tagged union — the type's identity is "success-of-T"
  or "failure-of-TError" and there is nothing meaningful to mutate-with-replacement.
- Value equality silently binds Result equality to `TError`'s equality. If
  `TError` is a class with reference equality, two `Failure(...)` results that
  ought to be equal would compare unequal — a brittle commitment the library
  should not make on consumers' behalf.

## Public API

Namespace: `JoakimAnder.Toolbox.Results`.

### `Result<TError>` — void success

```
public readonly struct Result<TError> where TError : notnull
{
    // private state: byte _state (0=uninit, 1=success, 2=failure), TError? _error

    public static Result<TError> Success();
    public static Result<TError> Failure(TError error);

    // Conversions
    implicit from Failure<TError>          // see static Result.Failure helper below
    implicit from TError                   // ergonomic `return error;`

    public bool IsSuccess { get; }         // false when default
    public bool IsFailure { get; }         // false when default
    public bool IsDefault { get; }         // true when default(Result<TError>)

    public bool TryGetError(out TError error);
    public void Match(Action onSuccess, Action<TError> onFailure);
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<TError, TOut> onFailure);

    public Result<FError>     MapError<FError>(Func<TError, FError> map) where FError : notnull;
    public Result<TError>     Bind(Func<Result<TError>> next);
    public Result<T, TError>  Bind<T>(Func<Result<T, TError>> next);

    public void ThrowIfFailure();                              // default exception
    public void ThrowIfFailure(Func<TError, Exception> map);   // mapped exception
}
```

### `Result<T, TError>` — typed success

```
public readonly struct Result<T, TError> where TError : notnull
{
    // private state: byte _state, T? _value, TError? _error

    public static Result<T, TError> Success(T value);
    public static Result<T, TError> Failure(TError error);

    implicit from Success<T>
    implicit from Failure<TError>
    implicit from T                        // `return value;`
    implicit from TError                   // `return error;`

    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public bool IsDefault { get; }

    public bool TryGetValue(out T value, out TError error);
    public bool TryGetError(out TError error);

    public void Match(Action<T> onSuccess, Action<TError> onFailure);
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<TError, TOut> onFailure);

    public Result<U, TError>  Map<U>(Func<T, U> map);
    public Result<T, FError>  MapError<FError>(Func<TError, FError> map) where FError : notnull;
    public Result<U, TError>  Bind<U>(Func<T, Result<U, TError>> next);
    public Result<TError>     Bind(Func<T, Result<TError>> next);

    public T ValueOrThrow();                              // default exception
    public T ValueOrThrow(Func<TError, Exception> map);   // mapped exception
}
```

### Static `Result` helpers

```
public static class Result
{
    // Type-inference helpers for the implicit-conversion ambiguity case
    // (T == TError, or one assignable to the other).
    public static Success<T>      Success<T>(T value);
    public static Failure<TError> Failure<TError>(TError error) where TError : notnull;

    // Exception interop — in
    public static Result<T, TError> Try<T, TError>(
        Func<T> action,
        Func<Exception, TError> onException) where TError : notnull;

    public static Result<TError> Try<TError>(
        Action action,
        Func<Exception, TError> onException) where TError : notnull;

    public static Task<Result<T, TError>> TryAsync<T, TError>(
        Func<CancellationToken, Task<T>> action,
        Func<Exception, TError> onException,
        CancellationToken cancellationToken = default) where TError : notnull;

    public static Task<Result<TError>> TryAsync<TError>(
        Func<CancellationToken, Task> action,
        Func<Exception, TError> onException,
        CancellationToken cancellationToken = default) where TError : notnull;
}

// Carrier structs — exist solely to hop through the implicit conversion so the
// compiler can infer one half of the Result's type parameters at the call site.
public readonly struct Success<T>            { internal readonly T Value; }
public readonly struct Failure<TError> where TError : notnull { internal readonly TError Error; }
```

The carrier-struct pattern works because the implicit conversions
(`Success<T>` → `Result<T, TError>`, `Failure<TError>` → `Result<T, TError>`
and `Result<TError>`) are defined on the *Result* types, where `TError` is in
scope. The carriers themselves are agnostic — they cannot collide with each
other or with the direct `T` / `TError` conversions even when `T` equals
`TError`.

### `Task<Result<…>>` extension surface

A single non-generic `TaskResultExtensions` static class so `await
someCall().Map(...)` works directly:

```
public static class TaskResultExtensions
{
    // Result<T, TError>
    Task<Result<U, TError>>  Map      <T,U,TError>      (this Task<Result<T,TError>>, Func<T,U>);
    Task<Result<T, FError>>  MapError <T,TError,FError> (this Task<Result<T,TError>>, Func<TError,FError>);
    Task<Result<U, TError>>  Bind     <T,U,TError>      (this Task<Result<T,TError>>, Func<T, Result<U,TError>>);
    Task<Result<U, TError>>  BindAsync<T,U,TError>      (this Task<Result<T,TError>>, Func<T, Task<Result<U,TError>>>);
    Task<TOut>               Match    <T,TError,TOut>   (this Task<Result<T,TError>>, Func<T,TOut>, Func<TError,TOut>);
    Task<TOut>               MatchAsync<T,TError,TOut>  (this Task<Result<T,TError>>, Func<T,Task<TOut>>, Func<TError,Task<TOut>>);
    Task<T>                  ValueOrThrowAsync<T,TError>(this Task<Result<T,TError>>);
    Task<T>                  ValueOrThrowAsync<T,TError>(this Task<Result<T,TError>>, Func<TError,Exception>);

    // Result<TError> — symmetric set without the value-side
    Task<Result<FError>>     MapError <TError,FError>   (this Task<Result<TError>>, Func<TError,FError>);
    Task<Result<TError>>     Bind     <TError>          (this Task<Result<TError>>, Func<Result<TError>>);
    Task<Result<TError>>     BindAsync<TError>          (this Task<Result<TError>>, Func<Task<Result<TError>>>);
    Task<Result<T, TError>>  Bind     <T,TError>        (this Task<Result<TError>>, Func<Result<T,TError>>);
    Task<Result<T, TError>>  BindAsync<T,TError>        (this Task<Result<TError>>, Func<Task<Result<T,TError>>>);
    Task<TOut>               Match    <TError,TOut>     (this Task<Result<TError>>, Func<TOut>, Func<TError,TOut>);
    Task<TOut>               MatchAsync<TError,TOut>    (this Task<Result<TError>>, Func<Task<TOut>>, Func<TError,Task<TOut>>);
    Task                     ThrowIfFailureAsync<TError>(this Task<Result<TError>>);
    Task                     ThrowIfFailureAsync<TError>(this Task<Result<TError>>, Func<TError,Exception>);
}
```

Each extension `await`s the task with `ConfigureAwait(false)` and delegates to
the sync operator on the awaited Result. They do **not** introduce or thread
cancellation tokens — the library composes over the *Result* type, not over
execution machinery. Cancellation is each step's responsibility, threaded in
at construction.

### File layout

Folder: `src/JoakimAnder.Toolbox/Results/`.

| File | Contains |
|---|---|
| `Result.cs` | Static `Result` helpers; `Success<T>` / `Failure<TError>` carrier structs |
| `ResultOfTError.cs` | `Result<TError>` struct |
| `ResultOfTAndTError.cs` | `Result<T, TError>` struct |
| `Result.Try.cs` | `partial class Result` with `Try` / `TryAsync` (sync + async, void + typed) |
| `TaskResultExtensions.cs` | All `Task<Result<…>>` extension methods |

The partial split on `Result` keeps the small inference helpers and the
larger exception-interop surface in separate files, each independently
legible.

## Semantics

### The three states and the default-state guard

Every `Result<…>` is in exactly one of three states: **uninitialized**
(`_state == 0`, only from `default`), **success** (`1`), or **failure**
(`2`). Every operation except the three state-inspection properties
(`IsSuccess`, `IsFailure`, `IsDefault`) throws
`InvalidOperationException("Result is uninitialized.")` when called on an
uninitialized Result. Factories and implicit conversions never produce one.

The only ways a `default` Result enters real code are uninitialized fields,
`out` params before assignment, `dict.TryGetValue` misses on a Result-valued
dictionary, and unusual `default` parameter values. The loud throw beats a
silent ghost-success in every one of those cases — the same trade-off
`ImmutableArray<T>.IsDefault` makes.

### Implicit conversion ambiguity

When `T` and `TError` are the same type, or one is assignable to the other
(`Result<string, string>`, `Result<object, Exception>`), the compiler cannot
choose between `implicit from T` and `implicit from TError`. This surfaces as
a **compile error at the return site**, which is the desired failure mode —
the call site disambiguates via:

- `Result<T, TError>.Success(value)` / `Result<T, TError>.Failure(error)` —
  explicit static factories on the Result type.
- `Result.Success(value)` / `Result.Failure(error)` — the inferred-type carrier
  helpers, whose conversions go through distinct `Success<T>` / `Failure<TError>`
  carrier structs and therefore never collide.

Documented in the XML doc on each implicit conversion and in the README.

### Reading

- `Match` — invokes exactly one of `onSuccess` / `onFailure`. The returning
  overload returns whatever the selected delegate returns; the void overload
  returns nothing.
- `TryGetValue(out T value, out TError error)` — returns `true` on success
  (sets `value`; `error` is `default!`); returns `false` on failure (sets
  `error`; `value` is `default!`). NRT attributes:
  `[MaybeNullWhen(false)] value`, `[MaybeNullWhen(true)] error`, so the
  pattern `if (r.TryGetValue(out var v, out var e)) { … } else { … }` flow-analyzes
  correctly.
- `TryGetError(out TError error)` — symmetric for the void variant; `true` iff
  failure.

### Operators

- `Map<U>(map)` — success → `Result<U, TError>.Success(map(value))`; failure →
  `Result<U, TError>.Failure(error)` (mapper not invoked); default → throws.
- `MapError<FError>(map)` — symmetric on the error side.
- `Bind<U>(next)` — success → `next(value)` (itself a `Result`); failure →
  propagates as `Result<U, TError>.Failure(error)`; default → throws.

If the user's mapper or binder itself throws, the exception propagates. Map
and Bind do **not** catch — a throw inside the lambda is a programmer error,
not a domain failure. If the lambda might fail with a domain error, use `Bind`
and return a `Result.Failure(...)` from the lambda (or wrap the lambda in
`Result.Try`).

### Async chain

`Task<Result<…>>` extension methods `await task.ConfigureAwait(false)` and
delegate to the sync operator on the awaited Result. They do not introduce or
thread cancellation tokens. The library composes over the *Result* type, not
over execution machinery.

### `Try` / `TryAsync` catch policy

Catches every `Exception` and invokes `onException(ex)`, packaging the result
as `Result<…>.Failure(…)`. **Exception:** `OperationCanceledException` (and
its derived `TaskCanceledException`) is **rethrown unchanged**. Cancellation
must remain cancellation — swallowing it into a Result silently breaks the
async-cancellation contract and confuses outer awaits.

The `TryAsync` overloads accept a `CancellationToken` and thread it into the
`Func<CancellationToken, Task<T>>` factory (same shape as `FanOut`), so
cancellation is wired in by construction.

If `onException` itself throws, that exception propagates — the catch handler
is now the source of failure.

### `ValueOrThrow` / `ThrowIfFailure`

- `ValueOrThrow()` (no mapper) — success: returns value. Failure: throws
  `InvalidOperationException($"Result was a failure: {error}")` (uses
  `TError.ToString()`). Default: throws the uninitialized exception.
- `ValueOrThrow(map)` — failure: throws `map(error)`. If `map` returns `null`,
  throws `InvalidOperationException("Mapped exception was null.")` rather than
  letting `throw null` surface as a `NullReferenceException`.
- `ThrowIfFailure` — void analog for `Result<TError>`. Same semantics; on
  success, returns normally.
- `ValueOrThrowAsync` / `ThrowIfFailureAsync` — await first, then delegate to
  the sync method.

### Equality

Not implemented beyond the C# `struct` default (field-wise equality, no
`IEquatable<…>`, no `==` operator). A deliberate non-commitment: Result
equality is only useful in tests, where the right comparison is usually
content-level (`Match` / `TryGetValue` on both sides). Implementing
`IEquatable<…>` would force a position on `TError`'s equality and imply
runtime contracts the library should not promise.

## Composition with `FanOut`

Two patterns shipped, one explicit non-goal.

### Pattern 1 — wrap the whole `FanOut` call

Zero new code. `FanOut`'s "first fault rethrown unwrapped" contract feeds
directly into `Result.TryAsync`:

```csharp
var result = await Result.TryAsync(
    ct => new FanOut()
        .Add(c => GetUserAsync(c))
        .Add(c => GetOrdersAsync(c))
        .WhenAll(c),
    ex => MapToDomainError(ex),
    cancellationToken);
```

Because the original exception type is preserved, the mapper can `switch` on
it precisely (`HttpRequestException`, `TimeoutException`, …). This is the
**90% case**.

### Pattern 2 — each op returns a `Result<T, TError>`, collapse with `Bind`

```csharp
var (userR, ordersR) = await new FanOut()
    .Add(c => GetUserAsync(c))         // Task<Result<User, ApiError>>
    .Add(c => GetOrdersAsync(c))       // Task<Result<Orders, ApiError>>
    .WhenAll(cancellationToken);

Result<(User, Orders), ApiError> result =
    userR.Bind(u => ordersR.Map(o => (u, o)));
```

**Limitation, documented:** `FanOut` cannot fail-fast on Result-failures —
from its view, a `Result.Failure(...)` is a successfully completed `Task`. A
slow sibling will **not** be cancelled when another op's Result is a failure.
Use pattern 1 if you want fail-fast cancellation on typed errors too.

### Pattern 3 — explicit non-goal

A Result-aware `FanOut` overload (each `Add` takes
`Func<CT, Task<Result<T, TError>>>`, the engine treats `Result.Failure` as a
fault, cancels siblings, terminal returns `Task<Result<(T1, T2, …), TError>>`)
is **out of scope**. The TError-must-match-across-the-ladder constraint and
the engine changes belong in their own design. Possible future sub-project.

## Edge cases

- `default(Result<…>)` — every operation except `IsSuccess` / `IsFailure` /
  `IsDefault` throws `InvalidOperationException("Result is uninitialized.")`.
- `T` assignable to `TError` (or vice versa) — compile error at the return
  site on implicit conversion; caller disambiguates via the explicit
  factories or the inferred-type carrier helpers.
- `T` is a reference type and `null` is passed — `Success(null!)` is permitted
  (no `where T : notnull`); callers who want non-null successes can constrain
  on their own side. Documented.
- `TError` cannot be null (the `where TError : notnull` constraint).
- Mapper or binder throws inside `Map` / `Bind` — exception propagates. Not a
  domain failure.
- `Result.Try` / `TryAsync` catches every `Exception` and maps; rethrows
  `OperationCanceledException` (and `TaskCanceledException`) unchanged.
- `onException` itself throws inside `Try` — that exception propagates.
- `ValueOrThrow(map)` where `map` returns `null` — throws
  `InvalidOperationException("Mapped exception was null.")` instead of
  `throw null`.
- `Task<Result<…>>` task that faults — `await` rethrows the exception; the
  extension does not swallow.
- `Task<Result<…>>` task that returns `default(Result<…>)` — extension
  delegates to the sync operator, which throws the uninitialized exception.
  Loud failure preserved through the async boundary.

## Testing

xUnit, built-in assertions only (repo convention). Tests in
`tests/JoakimAnder.Toolbox.Tests/Results/`.

Per Result type:

- Factories and implicit conversions produce the expected state.
- `IsSuccess` / `IsFailure` / `IsDefault` correct in all three states.
- `Match` (both overloads) invokes the right branch with the right argument;
  the returning overload returns the right value.
- `TryGetValue` / `TryGetError` — assignment semantics and NRT flow-analysis
  (verified by writing the consumer pattern in the test code itself).
- `Map` / `MapError` / `Bind` — success applies the lambda; failure skips it;
  default throws.
- `default` state — every operation throws `InvalidOperationException` with
  the documented message.
- `ValueOrThrow` / `ThrowIfFailure` — success returns / void-returns; failure
  throws the default exception; custom mapper invoked; null-mapper-result
  guarded.

`Result.Try` / `TryAsync`:

- Successful action → `Result.Success`.
- Throwing action → `Result.Failure` with the mapped error.
- `OperationCanceledException` thrown by action → rethrown, **not** caught.
- `TaskCanceledException` thrown → rethrown (the OCE-derived case).
- `onException` throws → that exception propagates.
- The `CancellationToken` is threaded into the `TryAsync` action factory.

`TaskResultExtensions`:

- Each extension correctly delegates after `await`.
- `Map` / `Bind` / `MapError` preserve the failure path through `await`.
- `BindAsync` (Result side and Task side) chains correctly across `await`
  boundaries — a multi-step pipeline test that exercises both sync and async
  binds.
- `MatchAsync` invokes the right async branch.

FanOut composition:

- Pattern 1 — full `Result.TryAsync` wrap around two FanOut'd ops where one
  throws; assert the mapper sees the original exception type and the result
  is `Failure`.
- Pattern 2 — two FanOut'd ops where one returns `Result.Failure(...)`;
  assert the collapse with `Bind` yields the first-failure; **also** assert
  that a slow sibling was *not* cancelled (this documents pattern 2's
  limitation as a behavioural test, so a future change cannot silently shift
  it).

## Examples

One snippet in `examples/JoakimAnder.Toolbox.Examples`: a boundary method
that calls two async dependencies via `FanOut` (pattern 1), maps thrown
exceptions into a small caller-defined `ApiError` sealed-record DU, and
shows the boundary handler `switch`ing exhaustively on the DU cases — the
end-to-end "explicit failure flow at boundaries" story.

## Scope

In scope:

- `Result<TError>` and `Result<T, TError>` `readonly struct`s.
- Static `Result` helpers (`Success<T>` / `Failure<TError>` carrier helpers;
  `Try` / `TryAsync` sync + async, void + typed).
- `TaskResultExtensions` for `Task<Result<…>>` operators.
- xUnit tests as described.
- One runnable example showing the boundary pattern and `FanOut` pattern 1.
- Update `README.md` ("What's in the box") and `CHANGELOG.md`.

Out of scope (YAGNI / deferred):

- `Tap` / `TapError` — side-effect operators. Often requested, rarely useful
  in practice; add later if a real need surfaces.
- `Ensure` / `Validate` — predicate-driven failure injection. Easy to write
  at call sites without library support.
- `Sequence` / `Traverse` — collection-of-Results combinators.
- LINQ query syntax (`SelectMany` overloads).
- `Validation<T, TError>` / error aggregation — first-fault is intentional;
  aggregation is a different shape.
- Result-aware `FanOut` overload — its own sub-project (see Pattern 3 above).
- Built-in `Error` / `Code` type — explicitly rejected.
- `IEquatable<Result<…>>` / `==` operator — not committed.

## Non-goals

- Replacing exceptions for truly exceptional cases. The OCE-rethrow policy is
  the wedge that keeps cancellation honest, and the library's framing is
  "explicit failure at *boundaries*," not "no `throw` anywhere."
- Being a railway-oriented programming framework. `Bind` exists to make
  cross-boundary composition tractable, not to make `Result` the primary
  programming style.

## Open questions

None at design time. Implementation will need to pick exact NRT attribute
placement on `TryGetValue` / `TryGetError` (likely `[MaybeNullWhen(false)]` on
the value and `[MaybeNullWhen(true)]` on the error; verified during
implementation against the analyzer's actual flow-analysis output).
