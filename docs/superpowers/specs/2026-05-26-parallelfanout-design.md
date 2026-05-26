# ParallelFanout — Design Spec

**Date:** 2026-05-26
**Sub-project:** ParallelFanout (see [docs/ROADMAP.md](../../ROADMAP.md))
**Status:** Approved, ready for implementation planning.

## Goal

Run a small set of asynchronous operations concurrently where **all must succeed**, and the moment one faults, **cancel the rest** so no time or resources are wasted finishing work whose result will be thrown away.

## The gap

The BCL has no clean primitive for this:

- `Task.WhenAll(t1, t2)` waits for *every* task to finish even after one has faulted. A long-running sibling keeps consuming resources after the outcome is already decided. It also only surfaces the *first* exception and the tasks are already started, so a cancellation token cannot be injected.
- `Parallel.ForEachAsync` fails fast and threads a token, but returns `void` — it is built for homogeneous collections, not a fixed heterogeneous set returning typed results.

Doing it by hand means wiring a linked `CancellationTokenSource`, threading its token into every operation, cancelling on the first fault, observing the siblings so none are orphaned, disposing the source, and untangling the real failure from the cancellation noise. That boilerplate is exactly what this utility encapsulates.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Core semantics | Fail-fast: first fault cancels siblings via a linked token | The whole point — stop wasting work once the outcome is decided |
| Operation form | Factories (`Func<CancellationToken, Task>` / `…Task<T>`), never started tasks | A token cannot be injected into an already-running task; factories let the utility start each op with a token it controls |
| Primary API | Instanceable, immutable type-accumulating builder (`FanOut` … `FanOut<T1..T8>`) | User preference; keeps full compile-time tuple typing across arities |
| Secondary API | Static `WhenAll` overloads (void + typed), 2–8, plus an `IEnumerable` void overload | Quick one-liner / dynamic counts without the builder |
| Result shape | Typed `ValueTuple` for result-producing ops, in add-order | Heterogeneous "get all of these back" case |
| Void ops | `Add(Func<CancellationToken, Task>)` — must succeed, contributes no tuple slot | Original priority: side-effecting work where you only care that it succeeded |
| Failure surface | First genuine fault rethrown **unwrapped**; sibling cancellations discarded | Most ergonomic — `catch (HttpRequestException)` works; first error wins |
| Outer cancellation | If the caller's own token cancels (no op faulted) → `OperationCanceledException` | Standard cancellation contract, distinct from a fault |
| Sibling handling | Cancel, then **await every sibling** (observe, never orphan), discarding their `OperationCanceledException` | No unobserved-exception or leaked-task hazards; relies on cooperative cancellation, which the use case already assumes |
| Arity ceiling | 8 | Comfortable `ValueTuple` range; beyond 8 use the void `IEnumerable` overload |
| Ladder source | Hand-written | Mechanical, one-time, fully debuggable; no build-time codegen plumbing |
| Builder type kind | `readonly struct` | Immutable, zero heap allocation per `Add` |
| Namespace | `JoakimAnder.Toolbox.Threading` | Mirrors `System.Threading.Tasks`; groups concurrency utilities as the toolbox grows |
| Terminal method | `WhenAll(CancellationToken = default)` | Mirrors the static method and the `Task.WhenAll` mental model |
| Dependencies | None beyond the BCL; plain arrays internally | Keeps the package dependency-free and AOT-clean |

### Why not a Roslyn source generator for the ladder

A Roslyn source generator was considered for emitting the arity ladder. It is the **wrong tool here**: the Foundation packs the generator into the NuGet's `analyzers/dotnet/cs`, so it runs in *consumers'* compilations. `FanOut` is the library's own public type, already compiled into `JoakimAnder.Toolbox.dll`; a generator emitting it would emit it a *second* time into each consumer's assembly → `CS0433` "type exists in both" ambiguity, breaking consumer builds. Generating the library's own fixed ladder is a build-time templating job (hand-written or T4), not a Roslyn-generator job. Roslyn source generators are reserved for the roadmap's DI sub-project (#5), where they legitimately emit code into consumer compilations based on consumer attributes — and a fixed-ladder generator would not de-risk that work anyway (its hard parts are semantic analysis and the incremental pipeline, which a fixed ladder never exercises).

## Public API

Namespace: `JoakimAnder.Toolbox.Threading`.

### Builder (primary)

`FanOut` is an instanceable, immutable `readonly struct`. Following the `Func<>` / `ValueTuple<>` convention, the arity variants are same-named generic types:

```
FanOut                          // arity 0 — new FanOut() or FanOut.Create()
FanOut<T1>                      // arity 1
FanOut<T1, T2> … FanOut<T1..T8> // up to arity 8
```

Each variant exposes three members:

- `FanOut<…, TNext> Add<TNext>(Func<CancellationToken, Task<TNext>> operation)` — adds a result-producing op; returns the **next** arity.
- `FanOut<…> Add(Func<CancellationToken, Task> operation)` — adds a void "must also succeed" op; returns the **same** arity.
- `WhenAll(CancellationToken cancellationToken = default)` — terminal:
  - arity 0 → `Task`
  - arity 1 → `Task<T1>`
  - arity ≥ 2 → `Task<(T1, …)>`

At arity 8, `Add<TNext>` is no longer offered (ceiling); `Add` (void) and `WhenAll` remain.

```csharp
using JoakimAnder.Toolbox.Threading;

var (user, orders) = await new FanOut()
    .Add(ct => GetUserAsync(ct))
    .Add(ct => GetOrdersAsync(ct))
    .Add(ct => AuditAsync(ct))      // void — must succeed, no tuple slot
    .WhenAll(cancellationToken);
```

Each `Add` returns a new value, so an intermediate builder is reusable/branchable. The builder type changes on every `Add`, so it **cannot** be accumulated in a loop or conditionally — use the static `IEnumerable` overload for dynamically sized sets.

`new FanOut()`, `default(FanOut)`, and `FanOut.Create()` all yield an empty arity-0 builder (internal arrays treated as empty when null).

### Static `WhenAll` (secondary convenience)

Static methods on the non-generic `FanOut` type:

**Void** (the original "all must succeed" priority):
```csharp
Task WhenAll(Func<CancellationToken, Task> op1,
             Func<CancellationToken, Task> op2,
             CancellationToken cancellationToken = default);
// … fixed arities through 8

Task WhenAll(IEnumerable<Func<CancellationToken, Task>> operations,
             CancellationToken cancellationToken = default);   // dynamic / >8
```

**Typed** (quick one-liner without the builder):
```csharp
Task<(T1, T2)> WhenAll<T1, T2>(Func<CancellationToken, Task<T1>> op1,
                               Func<CancellationToken, Task<T2>> op2,
                               CancellationToken cancellationToken = default);
// … fixed arities through 8
```

Every overload takes `CancellationToken cancellationToken = default` last (sidesteps the `params`-then-optional conflict).

## Shared engine

All public paths normalize to an ordered list of `Func<CancellationToken, Task>` and funnel through one private routine. Typed ops are wrapped so that, on completion, their result is captured into an ordered slot; void ops capture nothing.

Algorithm:

1. `using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)`.
2. Invoke every factory with `linked.Token`. Guard the invocation: a factory that throws synchronously, or returns a `null` Task, is turned into a faulted op rather than crashing the engine.
3. Drain with `Task.WhenAny` in a loop. On the **first genuinely faulted** op (`IsFaulted`), capture its exception (`ex.InnerException ?? ex`) and call `linked.Cancel()` so cooperating siblings wind down. Continue draining every remaining task — they are awaited and observed, never orphaned. Tasks that come back `IsCanceled` are collateral and ignored.
4. After all tasks settle:
   - a genuine fault was captured → **rethrow it unwrapped**;
   - else the **outer** `cancellationToken` is cancelled → `cancellationToken.ThrowIfCancellationRequested()` (`OperationCanceledException`);
   - else success → assemble captured results in add-order and return the tuple.

`Task.WhenAny`-in-a-loop is O(n²); irrelevant at n ≤ 8. No reflection; plain arrays internally → AOT-clean.

### Result capture & boxing

Typed results are captured into an ordered `object?[]` (each typed op wrapped as `async ct => box(await op(ct))`), then cast back (`(T1)values[0]`, …) when assembling the tuple. Boxing a handful of values is negligible for this "few operations" utility and is fully AOT-safe (no reflection, no dynamic codegen). The number of type parameters on a builder variant equals the number of typed slots, so tuple assembly indexes are known at each arity.

## Edge cases

- **Factory throws synchronously** (before returning a `Task`) → treated as a faulted op; becomes the trigger.
- **Factory returns `null`** → treated as a faulted op (`InvalidOperationException`).
- **Null factory argument / null `IEnumerable`** → `ArgumentNullException`, validated up front.
- **Empty set** (arity-0 `WhenAll`, or empty `IEnumerable`) → completes immediately; vacuously "all succeeded".
- **Outer token already cancelled at entry** → `OperationCanceledException`, no op is invoked-and-faulted.
- **Multiple near-simultaneous genuine faults** → the first observed wins; the rest arrive as cancellations once `linked.Cancel()` fires and are discarded.
- **Non-cooperative sibling** (ignores its token) → cannot be force-killed by anyone; it is awaited rather than orphaned. The contract assumes cooperative cancellation.
- **`default(FanOut)`** → behaves as an empty builder.

## Testing

xUnit, built-in assertions only (repo convention). In `tests/JoakimAnder.Toolbox.Tests`:

Builder:
- All ops succeed → tuple returned in add-order with correct values; void ops contribute no slot.
- Mixed typed + void all succeed → typed tuple correct, void op observed to have run.
- One op faults → that exception type propagates **unwrapped**; a deliberately slow sibling observes cancellation (assert via a flag set in its `catch (OperationCanceledException)`).
- Fault cancels a long sibling **quickly** → assert the call returns well under the sibling's full delay (proves the resource-saving claim).
- Outer token pre-cancelled, and cancelled mid-flight → `OperationCanceledException`; no op treated as faulting.
- Synchronous-throw factory, `null`-returning factory, null argument, empty builder.
- `default(FanOut).WhenAll()` completes.
- Arity coverage: at minimum exercise arity 2, a mid arity, and arity 8 to confirm the ladder wiring.

Static:
- Void fixed-arity and `IEnumerable` overloads: success, fault-with-cancellation, outer-cancel, empty, null.
- Typed fixed-arity overloads: success tuple, fault unwrapped.

## Examples

One snippet in `examples/JoakimAnder.Toolbox.Examples` showing the typed builder with two fetches plus a void audit op, where one fetch fails and a deliberately slow sibling is observed to cancel promptly — the exact scenario that motivated the utility.

## Scope

In scope:
- `FanOut` builder ladder (arity 0–8) in `JoakimAnder.Toolbox.Threading`.
- Static `WhenAll` overloads (void fixed 2–8 + `IEnumerable`; typed 2–8).
- Shared private engine.
- xUnit tests and one runnable example.
- Delete `src/JoakimAnder.Toolbox/Placeholder.cs` (this is the first real feature).
- Update `README.md` ("What's in the box") and `CHANGELOG.md`.

Out of scope (YAGNI / deferred):
- Throttling / `MaxDegreeOfParallelism` (the set is small and fixed; irrelevant here).
- Streaming / `IAsyncEnumerable` / progress reporting.
- Already-started-task overloads (cannot be cancelled — deliberately excluded).
- Loop/conditional builder composition (intrinsic to type-accumulation; the `IEnumerable` void overload covers dynamic counts).
- Integration with the planned `Result<T>` type (separate sub-project; fail-fast throw is the contract here).
- Any Roslyn source generator (see "Why not a Roslyn source generator").

## Non-goals

- Replacing `Parallel.ForEachAsync` for large homogeneous collections.
- Aggregating every genuine fault (first-fault-wins was chosen for ergonomics).

## Open questions

None at design time. One implementation detail is left to execution: the exact mechanics of guarding a `null`-returning factory versus a synchronous throw (both end as a faulted op; the wrapping differs).
