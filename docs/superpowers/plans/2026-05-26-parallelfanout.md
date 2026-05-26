# ParallelFanout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a fail-fast parallel-execution utility (`FanOut`) that runs async operations which must all succeed, cancelling the rest the moment one faults.

**Architecture:** One internal engine (`FanOutEngine`) runs an ordered set of `Func<CancellationToken, Task>` under a linked `CancellationTokenSource`, cancels siblings on the first genuine fault, observes every task, and rethrows the trigger unwrapped. Two public faces funnel into it: an immutable, type-accumulating builder (`FanOut`, `FanOut<T1>` … `FanOut<T1..T8>`) and static `WhenAll` convenience overloads. All in namespace `JoakimAnder.Toolbox.Threading`.

**Tech Stack:** C# / .NET 10, `readonly struct` builders, `ValueTuple` results, xUnit (built-in assertions). No external dependencies; plain arrays internally; AOT-clean.

**Spec:** [docs/superpowers/specs/2026-05-26-parallelfanout-design.md](../specs/2026-05-26-parallelfanout-design.md)

---

### Task 1: Engine + builder arity 0–2

**Goal:** The walking skeleton — the shared engine plus `FanOut` (arity 0), `FanOut<T1>`, and `FanOut<T1, T2>` (temporary ceiling), proving every fail-fast/cancellation semantic through arity 2.

**Files:**
- Create: `src/JoakimAnder.Toolbox/Threading/FanOutEngine.cs`
- Create: `src/JoakimAnder.Toolbox/Threading/FanOutArray.cs`
- Create: `src/JoakimAnder.Toolbox/Threading/FanOut.cs`
- Create: `src/JoakimAnder.Toolbox/Threading/FanOutBuilders.cs`
- Delete: `src/JoakimAnder.Toolbox/Placeholder.cs`
- Test: `tests/JoakimAnder.Toolbox.Tests/Threading/FanOutBuilderTests.cs`

**Acceptance Criteria:**
- [ ] Engine creates a linked CTS, invokes each factory with the linked token, cancels on first genuine fault, observes all tasks, rethrows the trigger unwrapped.
- [ ] Outer-token cancellation (no fault) surfaces as `OperationCanceledException`; sibling cancellations are discarded.
- [ ] Synchronous-throw and null-returning factories become faulted ops; `Add(null)` throws `ArgumentNullException`; empty builder completes.
- [ ] `new FanOut()`, `FanOut.Create()`, and `default(FanOut)` all behave as an empty arity-0 builder.
- [ ] Results returned in add-order; void ops run but contribute no tuple slot.
- [ ] `dotnet build -c Release` is warning-free.

**Verify:** `dotnet test --filter "FullyQualifiedName~FanOutBuilderTests"` → all pass; `dotnet build -c Release` → 0 warnings.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `tests/JoakimAnder.Toolbox.Tests/Threading/FanOutBuilderTests.cs`:

````csharp
using System.Diagnostics;
using JoakimAnder.Toolbox.Threading;
using Xunit;

namespace JoakimAnder.Toolbox.Tests.Threading;

public class FanOutBuilderTests
{
    [Fact]
    public async Task Returns_results_in_add_order_and_runs_void_ops()
    {
        var audited = false;
        var (a, b) = await new FanOut()
            .Add(_ => Task.FromResult("first"))
            .Add(_ => Task.FromResult(42))
            .Add(ct => { audited = true; return Task.CompletedTask; })
            .WhenAll();

        Assert.Equal("first", a);
        Assert.Equal(42, b);
        Assert.True(audited);
    }

    [Fact]
    public async Task First_fault_propagates_unwrapped_and_cancels_siblings()
    {
        var observed = false;
        var builder = new FanOut()
            .Add<int>(_ => Task.FromException<int>(new InvalidOperationException("boom")))
            .Add(async ct =>
            {
                try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
                catch (OperationCanceledException) { observed = true; throw; }
            });

        var sw = Stopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.WhenAll());
        sw.Stop();

        Assert.Equal("boom", ex.Message);
        Assert.True(observed, "slow sibling should have observed cancellation");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"should return promptly, took {sw.Elapsed}");
    }

    [Fact]
    public async Task Outer_cancellation_throws_operation_canceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var builder = new FanOut()
            .Add<int>(async ct => { await Task.Delay(TimeSpan.FromSeconds(30), ct); return 1; })
            .Add<int>(async ct => { await Task.Delay(TimeSpan.FromSeconds(30), ct); return 2; });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.WhenAll(cts.Token));
    }

    [Fact]
    public async Task Synchronous_throw_in_factory_is_treated_as_fault()
    {
        var builder = new FanOut()
            .Add<int>(_ => throw new InvalidOperationException("sync"))
            .Add(_ => Task.FromResult(2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.WhenAll());
        Assert.Equal("sync", ex.Message);
    }

    [Fact]
    public async Task Null_returning_factory_is_treated_as_fault()
    {
        var builder = new FanOut()
            .Add<int>(_ => null!)
            .Add(_ => Task.FromResult(2));

        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.WhenAll());
    }

    [Fact]
    public void Add_null_operation_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => new FanOut().Add((Func<CancellationToken, Task>)null!));
        Assert.Throws<ArgumentNullException>(() => new FanOut().Add<int>((Func<CancellationToken, Task<int>>)null!));
    }

    [Fact]
    public async Task Empty_builder_completes()
    {
        await new FanOut().WhenAll();
        await FanOut.Create().WhenAll();
        await default(FanOut).WhenAll();
    }
}
````

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~FanOutBuilderTests"`
Expected: FAIL — `FanOut` / namespace `JoakimAnder.Toolbox.Threading` does not exist (compile error).

- [ ] **Step 3: Create the array helper**

Create `src/JoakimAnder.Toolbox/Threading/FanOutArray.cs`:

```csharp
namespace JoakimAnder.Toolbox.Threading;

internal static class FanOutArray
{
    public static T[] Append<T>(T[]? source, T item)
    {
        if (source is null || source.Length == 0)
        {
            return [item];
        }

        var result = new T[source.Length + 1];
        Array.Copy(source, result, source.Length);
        result[source.Length] = item;
        return result;
    }

    public static T[] OrEmpty<T>(T[]? source) => source ?? [];
}
```

- [ ] **Step 4: Create the engine**

Create `src/JoakimAnder.Toolbox/Threading/FanOutEngine.cs`:

```csharp
using System.Runtime.ExceptionServices;

namespace JoakimAnder.Toolbox.Threading;

internal static class FanOutEngine
{
    public static async Task<object?[]> RunAsync(
        Func<CancellationToken, Task<object?>>[] resultOps,
        Func<CancellationToken, Task>[] voidOps,
        CancellationToken cancellationToken)
    {
        var total = resultOps.Length + voidOps.Length;
        if (total == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;

        var resultTasks = new Task<object?>[resultOps.Length];
        for (var i = 0; i < resultOps.Length; i++)
        {
            resultTasks[i] = InvokeResult(resultOps[i], token);
        }

        var remaining = new List<Task>(total);
        remaining.AddRange(resultTasks);
        for (var i = 0; i < voidOps.Length; i++)
        {
            remaining.Add(Invoke(voidOps[i], token));
        }

        Exception? trigger = null;
        while (remaining.Count > 0)
        {
            var completed = await Task.WhenAny(remaining).ConfigureAwait(false);
            remaining.Remove(completed);

            if (trigger is null && completed.IsFaulted)
            {
                var aggregate = completed.Exception!;
                trigger = aggregate.InnerExceptions.Count == 1 ? aggregate.InnerExceptions[0] : aggregate;
                if (!linked.IsCancellationRequested)
                {
                    linked.Cancel();
                }
            }
        }

        if (trigger is not null)
        {
            ExceptionDispatchInfo.Throw(trigger);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var results = new object?[resultTasks.Length];
        for (var i = 0; i < resultTasks.Length; i++)
        {
            results[i] = resultTasks[i].Result;
        }

        return results;
    }

    public static Func<CancellationToken, Task<object?>> Box<T>(Func<CancellationToken, Task<T>> operation)
        => async ct =>
        {
            var task = operation(ct)
                ?? throw new InvalidOperationException("Operation factory returned a null Task.");
            return await task.ConfigureAwait(false);
        };

    private static Task<object?> InvokeResult(Func<CancellationToken, Task<object?>> op, CancellationToken token)
    {
        try
        {
            return op(token)
                ?? Task.FromException<object?>(new InvalidOperationException("Operation factory returned a null Task."));
        }
        catch (Exception ex)
        {
            return Task.FromException<object?>(ex);
        }
    }

    private static Task Invoke(Func<CancellationToken, Task> op, CancellationToken token)
    {
        try
        {
            return op(token)
                ?? Task.FromException(new InvalidOperationException("Operation factory returned a null Task."));
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }
}
```

- [ ] **Step 5: Create the arity-0 builder**

Create `src/JoakimAnder.Toolbox/Threading/FanOut.cs`:

```csharp
namespace JoakimAnder.Toolbox.Threading;

/// <summary>
/// Fail-fast fan-out of asynchronous operations that must all succeed. The first
/// operation to fault cancels the linked token handed to the rest, and its
/// exception is rethrown unwrapped. Immutable: each <c>Add</c> returns a new builder.
/// </summary>
public readonly partial struct FanOut
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    /// <summary>Creates an empty builder. Equivalent to <c>new FanOut()</c>.</summary>
    public static FanOut Create() => default;

    /// <summary>Adds a result-producing operation; returns the next-arity builder.</summary>
    public FanOut<T1> Add<T1>(Func<CancellationToken, Task<T1>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    /// <summary>Adds a void "must also succeed" operation; returns the same-arity builder.</summary>
    public FanOut Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut(_results, FanOutArray.Append(_voids, operation));
    }

    /// <summary>Runs all added operations fail-fast; completes when all succeed.</summary>
    public Task WhenAll(CancellationToken cancellationToken = default)
        => FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken);
}
```

- [ ] **Step 6: Create the generic builders (arity 1 and 2)**

Create `src/JoakimAnder.Toolbox/Threading/FanOutBuilders.cs`:

```csharp
namespace JoakimAnder.Toolbox.Threading;

/// <summary>Fan-out builder carrying one result type. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2> Add<T2>(Func<CancellationToken, Task<T2>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    public FanOut<T1> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<T1> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return (T1)r[0]!;
    }
}

/// <summary>Fan-out builder carrying two result types. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    // NOTE (Task 1 interim): arity 2 is the temporary ceiling — no Add<T3> yet.
    // Task 2 adds the Add<T3> method here once FanOut<T1, T2, T3> exists.

    public FanOut<T1, T2> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!);
    }
}
```

- [ ] **Step 7: Delete the placeholder**

Run: `git rm src/JoakimAnder.Toolbox/Placeholder.cs`

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~FanOutBuilderTests"`
Expected: PASS (all 7 tests).

- [ ] **Step 9: Confirm a clean build**

Run: `dotnet build -c Release`
Expected: Build succeeded, **0 warnings**. (Fix any analyzer warning before committing.)

- [ ] **Step 10: Commit**

```bash
git add src/JoakimAnder.Toolbox/Threading tests/JoakimAnder.Toolbox.Tests/Threading
git add -u src/JoakimAnder.Toolbox/Placeholder.cs
git commit -m "feat(threading): add FanOut engine and builder arity 0-2"
```

---

### Task 2: Builder arity 3–8

**Goal:** Extend the builder ladder to arity 8 by adding `Add<T3>` to `FanOut<T1, T2>` and defining `FanOut<T1, T2, T3>` … `FanOut<T1..T8>`, with arity 8 as the ceiling (no further `Add<TNext>`).

**Files:**
- Modify: `src/JoakimAnder.Toolbox/Threading/FanOutBuilders.cs`
- Test: `tests/JoakimAnder.Toolbox.Tests/Threading/FanOutBuilderTests.cs`

**Acceptance Criteria:**
- [ ] Each arity k (3 ≤ k ≤ 7) has `Add<Tk+1>` returning arity k+1, `Add(void)` returning arity k, and `WhenAll` returning `Task<(T1..Tk)>`.
- [ ] Arity 8 (`FanOut<T1..T8>`) has `Add(void)` and `WhenAll` but **no** `Add<TNext>`.
- [ ] `FanOut<T1, T2>` gains `Add<T3>`.
- [ ] Arity-3 and arity-8 builds return all results in add-order.

**Verify:** `dotnet test --filter "FullyQualifiedName~FanOutBuilderTests"` → all pass; `dotnet build -c Release` → 0 warnings.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Append to `tests/JoakimAnder.Toolbox.Tests/Threading/FanOutBuilderTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task Arity_three_returns_results_in_order()
    {
        var (a, b, c) = await new FanOut()
            .Add(_ => Task.FromResult(1))
            .Add(_ => Task.FromResult("two"))
            .Add(_ => Task.FromResult(3.0))
            .WhenAll();

        Assert.Equal(1, a);
        Assert.Equal("two", b);
        Assert.Equal(3.0, c);
    }

    [Fact]
    public async Task Arity_eight_returns_all_results_in_order()
    {
        var (r1, r2, r3, r4, r5, r6, r7, r8) = await new FanOut()
            .Add(_ => Task.FromResult(1))
            .Add(_ => Task.FromResult(2))
            .Add(_ => Task.FromResult(3))
            .Add(_ => Task.FromResult(4))
            .Add(_ => Task.FromResult(5))
            .Add(_ => Task.FromResult(6))
            .Add(_ => Task.FromResult(7))
            .Add(_ => Task.FromResult(8))
            .WhenAll();

        Assert.Equal((1, 2, 3, 4, 5, 6, 7, 8), (r1, r2, r3, r4, r5, r6, r7, r8));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~FanOutBuilderTests"`
Expected: FAIL — arity-3 chain (`.Add` on `FanOut<T1, T2>` returning a result type) does not compile (`Add<T3>` missing).

- [ ] **Step 3: Add `Add<T3>` to `FanOut<T1, T2>`**

In `FanOutBuilders.cs`, in `FanOut<T1, T2>`, replace the interim NOTE comment with this method:

```csharp
    public FanOut<T1, T2, T3> Add<T3>(Func<CancellationToken, Task<T3>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }
```

- [ ] **Step 4: Define `FanOut<T1, T2, T3>` (the exemplar for arities 3–7)**

Append to `FanOutBuilders.cs`:

```csharp
/// <summary>Fan-out builder carrying three result types. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2, T3>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2, T3, T4> Add<T4>(Func<CancellationToken, Task<T4>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    public FanOut<T1, T2, T3> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2, T3)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!);
    }
}
```

- [ ] **Step 5: Define `FanOut<T1, T2, T3, T4>` through `FanOut<T1..T7>` by the same mechanical rule**

For each arity k from 4 to 7, append a struct identical in shape to `FanOut<T1, T2, T3>` above, transformed only by count:
- Type name: `FanOut<T1, …, Tk>` (k type parameters).
- `Add<T{k+1}>` returns `new FanOut<T1, …, Tk, T{k+1}>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids)`.
- `Add(Func<CancellationToken, Task>)` returns `new FanOut<T1, …, Tk>(_results, FanOutArray.Append(_voids, operation))`.
- `WhenAll` returns `Task<(T1, …, Tk)>` with body `return ((T1)r[0]!, (T2)r[1]!, …, (Tk)r[k-1]!);`.
- Fields, constructor, and null-checks are byte-for-byte the same as the exemplar.

So `FanOut<T1, T2, T3, T4>.Add<T5>` → `FanOut<T1, T2, T3, T4, T5>`, … up to `FanOut<T1..T7>.Add<T8>` → `FanOut<T1..T8>`.

- [ ] **Step 6: Define `FanOut<T1..T8>` (the ceiling — no `Add<TNext>`)**

Append to `FanOutBuilders.cs`:

```csharp
/// <summary>Fan-out builder carrying eight result types (the arity ceiling). See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2, T3, T4, T5, T6, T7, T8>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2, T3, T4, T5, T6, T7, T8> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4, T5, T6, T7, T8>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2, T3, T4, T5, T6, T7, T8)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!, (T6)r[5]!, (T7)r[6]!, (T8)r[7]!);
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~FanOutBuilderTests"`
Expected: PASS (all 9 tests).

- [ ] **Step 8: Confirm a clean build**

Run: `dotnet build -c Release`
Expected: Build succeeded, **0 warnings**.

- [ ] **Step 9: Commit**

```bash
git add src/JoakimAnder.Toolbox/Threading/FanOutBuilders.cs tests/JoakimAnder.Toolbox.Tests/Threading/FanOutBuilderTests.cs
git commit -m "feat(threading): extend FanOut builder ladder to arity 8"
```

---

### Task 3: Static void `WhenAll` overloads

**Goal:** Static convenience overloads on `FanOut` for the void "all must succeed" case — fixed arity 2–8 plus an `IEnumerable` overload for dynamic/large counts.

**Files:**
- Create: `src/JoakimAnder.Toolbox/Threading/FanOut.StaticVoid.cs`
- Test: `tests/JoakimAnder.Toolbox.Tests/Threading/FanOutStaticVoidTests.cs`

**Acceptance Criteria:**
- [ ] Fixed-arity void `WhenAll(op1, op2, …, ct = default)` for 2–8, each delegating to the engine.
- [ ] `WhenAll(IEnumerable<Func<CancellationToken, Task>> operations, ct = default)`: null collection → `ArgumentNullException`; null element → `ArgumentException`; empty → completes.
- [ ] Fault cancels siblings and rethrows unwrapped; outer cancel → `OperationCanceledException`.

**Verify:** `dotnet test --filter "FullyQualifiedName~FanOutStaticVoidTests"` → all pass; `dotnet build -c Release` → 0 warnings.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `tests/JoakimAnder.Toolbox.Tests/Threading/FanOutStaticVoidTests.cs`:

````csharp
using JoakimAnder.Toolbox.Threading;
using Xunit;

namespace JoakimAnder.Toolbox.Tests.Threading;

public class FanOutStaticVoidTests
{
    [Fact]
    public async Task Fixed_arity_all_succeed()
    {
        var count = 0;
        await FanOut.WhenAll(
            _ => { Interlocked.Increment(ref count); return Task.CompletedTask; },
            _ => { Interlocked.Increment(ref count); return Task.CompletedTask; },
            _ => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Fault_cancels_siblings_and_rethrows_unwrapped()
    {
        var observed = false;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => FanOut.WhenAll(
            _ => Task.FromException(new InvalidOperationException("boom")),
            async ct =>
            {
                try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
                catch (OperationCanceledException) { observed = true; throw; }
            }));

        Assert.Equal("boom", ex.Message);
        Assert.True(observed);
    }

    [Fact]
    public async Task Outer_cancellation_throws_operation_canceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FanOut.WhenAll(
            async ct => await Task.Delay(TimeSpan.FromSeconds(30), ct),
            async ct => await Task.Delay(TimeSpan.FromSeconds(30), ct),
            cts.Token));
    }

    [Fact]
    public async Task Enumerable_overload_runs_all()
    {
        var count = 0;
        var ops = Enumerable.Range(0, 5)
            .Select(_ => (Func<CancellationToken, Task>)(_ => { Interlocked.Increment(ref count); return Task.CompletedTask; }))
            .ToList();

        await FanOut.WhenAll(ops);

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task Enumerable_overload_empty_completes()
    {
        await FanOut.WhenAll(Enumerable.Empty<Func<CancellationToken, Task>>());
    }

    [Fact]
    public async Task Enumerable_overload_null_collection_throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => FanOut.WhenAll((IEnumerable<Func<CancellationToken, Task>>)null!));
    }

    [Fact]
    public async Task Enumerable_overload_null_element_throws()
    {
        var ops = new Func<CancellationToken, Task>[] { _ => Task.CompletedTask, null! };
        await Assert.ThrowsAsync<ArgumentException>(() => FanOut.WhenAll(ops));
    }
}
````

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~FanOutStaticVoidTests"`
Expected: FAIL — static `FanOut.WhenAll` overloads do not exist (compile error).

- [ ] **Step 3: Create the static void overloads**

Create `src/JoakimAnder.Toolbox/Threading/FanOut.StaticVoid.cs`:

```csharp
namespace JoakimAnder.Toolbox.Threading;

public readonly partial struct FanOut
{
    private static readonly Func<CancellationToken, Task<object?>>[] NoResults = [];

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        return FanOutEngine.RunAsync(NoResults, [op1, op2], cancellationToken);
    }

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        Func<CancellationToken, Task> op3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        return FanOutEngine.RunAsync(NoResults, [op1, op2, op3], cancellationToken);
    }

    // Arities 4–8: repeat the exact pattern above. For arity k, take k parameters
    // op1..opk (each Func<CancellationToken, Task>), ThrowIfNull each, and call
    // FanOutEngine.RunAsync(NoResults, [op1, .., opk], cancellationToken).
    // Shown fully for arity 8 below as the ceiling reference; write 4, 5, 6, 7 the same way.

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        Func<CancellationToken, Task> op3,
        Func<CancellationToken, Task> op4,
        Func<CancellationToken, Task> op5,
        Func<CancellationToken, Task> op6,
        Func<CancellationToken, Task> op7,
        Func<CancellationToken, Task> op8,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        ArgumentNullException.ThrowIfNull(op5);
        ArgumentNullException.ThrowIfNull(op6);
        ArgumentNullException.ThrowIfNull(op7);
        ArgumentNullException.ThrowIfNull(op8);
        return FanOutEngine.RunAsync(NoResults, [op1, op2, op3, op4, op5, op6, op7, op8], cancellationToken);
    }

    public static Task WhenAll(
        IEnumerable<Func<CancellationToken, Task>> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var array = operations.ToArray();
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i] is null)
            {
                throw new ArgumentException("Operations contains a null element.", nameof(operations));
            }
        }

        return FanOutEngine.RunAsync(NoResults, array, cancellationToken);
    }
}
```

> Write the arity 4, 5, 6, 7 overloads now, between arity 3 and arity 8, following the identical pattern (k parameters, k `ThrowIfNull` calls, collection expression `[op1, …, opk]`).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~FanOutStaticVoidTests"`
Expected: PASS (all 7 tests).

- [ ] **Step 5: Confirm a clean build**

Run: `dotnet build -c Release`
Expected: Build succeeded, **0 warnings**.

- [ ] **Step 6: Commit**

```bash
git add src/JoakimAnder.Toolbox/Threading/FanOut.StaticVoid.cs tests/JoakimAnder.Toolbox.Tests/Threading/FanOutStaticVoidTests.cs
git commit -m "feat(threading): add static void FanOut.WhenAll overloads"
```

---

### Task 4: Static typed `WhenAll` overloads

**Goal:** Static generic `WhenAll<T1..Tk>` overloads (arity 2–8) returning a `ValueTuple`, for the quick one-liner without the builder.

**Files:**
- Create: `src/JoakimAnder.Toolbox/Threading/FanOut.StaticTyped.cs`
- Test: `tests/JoakimAnder.Toolbox.Tests/Threading/FanOutStaticTypedTests.cs`

**Acceptance Criteria:**
- [ ] Generic `WhenAll<T1, T2>` … `WhenAll<T1..T8>` returning `Task<(T1, …)>`, each delegating to the engine via `FanOutEngine.Box`.
- [ ] Results returned in argument order; first fault rethrown unwrapped.
- [ ] `CancellationToken` is the last parameter, defaulted.

**Verify:** `dotnet test --filter "FullyQualifiedName~FanOutStaticTypedTests"` → all pass; `dotnet build -c Release` → 0 warnings.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `tests/JoakimAnder.Toolbox.Tests/Threading/FanOutStaticTypedTests.cs`:

```csharp
using JoakimAnder.Toolbox.Threading;
using Xunit;

namespace JoakimAnder.Toolbox.Tests.Threading;

public class FanOutStaticTypedTests
{
    [Fact]
    public async Task Arity_two_returns_tuple_in_order()
    {
        var (a, b) = await FanOut.WhenAll(
            _ => Task.FromResult("x"),
            _ => Task.FromResult(7));

        Assert.Equal("x", a);
        Assert.Equal(7, b);
    }

    [Fact]
    public async Task Arity_eight_returns_tuple_in_order()
    {
        var (r1, r2, r3, r4, r5, r6, r7, r8) = await FanOut.WhenAll(
            _ => Task.FromResult(1),
            _ => Task.FromResult(2),
            _ => Task.FromResult(3),
            _ => Task.FromResult(4),
            _ => Task.FromResult(5),
            _ => Task.FromResult(6),
            _ => Task.FromResult(7),
            _ => Task.FromResult(8));

        Assert.Equal((1, 2, 3, 4, 5, 6, 7, 8), (r1, r2, r3, r4, r5, r6, r7, r8));
    }

    [Fact]
    public async Task Fault_is_rethrown_unwrapped()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => FanOut.WhenAll(
            _ => Task.FromException<int>(new InvalidOperationException("boom")),
            _ => Task.FromResult(2)));

        Assert.Equal("boom", ex.Message);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~FanOutStaticTypedTests"`
Expected: FAIL — generic static `FanOut.WhenAll<…>` overloads do not exist (compile error).

- [ ] **Step 3: Create the static typed overloads**

Create `src/JoakimAnder.Toolbox/Threading/FanOut.StaticTyped.cs`:

```csharp
namespace JoakimAnder.Toolbox.Threading;

public readonly partial struct FanOut
{
    private static readonly Func<CancellationToken, Task>[] NoVoids = [];

    public static async Task<(T1, T2)> WhenAll<T1, T2>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        var r = await FanOutEngine.RunAsync(
            [FanOutEngine.Box(op1), FanOutEngine.Box(op2)], NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!);
    }

    public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        Func<CancellationToken, Task<T3>> op3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        var r = await FanOutEngine.RunAsync(
            [FanOutEngine.Box(op1), FanOutEngine.Box(op2), FanOutEngine.Box(op3)], NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!);
    }

    // Arities 4–7: repeat the exact pattern above. For arity k: k type params, k
    // Func<CancellationToken, Task<Ti>> parameters opi, k ThrowIfNull calls, the
    // collection expression [FanOutEngine.Box(op1), .., FanOutEngine.Box(opk)], and
    // return ((T1)r[0]!, .., (Tk)r[k-1]!). Arity 8 shown fully below as the reference.

    public static async Task<(T1, T2, T3, T4, T5, T6, T7, T8)> WhenAll<T1, T2, T3, T4, T5, T6, T7, T8>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        Func<CancellationToken, Task<T3>> op3,
        Func<CancellationToken, Task<T4>> op4,
        Func<CancellationToken, Task<T5>> op5,
        Func<CancellationToken, Task<T6>> op6,
        Func<CancellationToken, Task<T7>> op7,
        Func<CancellationToken, Task<T8>> op8,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        ArgumentNullException.ThrowIfNull(op5);
        ArgumentNullException.ThrowIfNull(op6);
        ArgumentNullException.ThrowIfNull(op7);
        ArgumentNullException.ThrowIfNull(op8);
        var r = await FanOutEngine.RunAsync(
            [
                FanOutEngine.Box(op1), FanOutEngine.Box(op2), FanOutEngine.Box(op3), FanOutEngine.Box(op4),
                FanOutEngine.Box(op5), FanOutEngine.Box(op6), FanOutEngine.Box(op7), FanOutEngine.Box(op8),
            ], NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!, (T6)r[5]!, (T7)r[6]!, (T8)r[7]!);
    }
}
```

> Write the arity 4, 5, 6, 7 overloads now, between arity 3 and arity 8, following the identical pattern.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~FanOutStaticTypedTests"`
Expected: PASS (all 3 tests).

- [ ] **Step 5: Confirm a clean build and full suite**

Run: `dotnet build -c Release` → 0 warnings.
Run: `dotnet test` → all tests across the solution pass.

- [ ] **Step 6: Commit**

```bash
git add src/JoakimAnder.Toolbox/Threading/FanOut.StaticTyped.cs tests/JoakimAnder.Toolbox.Tests/Threading/FanOutStaticTypedTests.cs
git commit -m "feat(threading): add static typed FanOut.WhenAll overloads"
```

---

### Task 5: Example + documentation

**Goal:** A runnable example demonstrating the motivating scenario, plus README and CHANGELOG updates.

**Files:**
- Modify: `examples/JoakimAnder.Toolbox.Examples/Program.cs`
- Modify: `README.md`
- Modify: `CHANGELOG.md`

**Acceptance Criteria:**
- [ ] Example uses the typed builder with two fetches + a void audit, where one fetch fails and the slow sibling is observed to cancel promptly.
- [ ] `dotnet run --project examples/JoakimAnder.Toolbox.Examples` prints the failure and the sibling-cancellation message, returning in well under the 30s sibling delay.
- [ ] README "What's in the box" documents `FanOut`.
- [ ] CHANGELOG `[Unreleased]` records the addition.

**Verify:** `dotnet run --project examples/JoakimAnder.Toolbox.Examples` → prints cancellation behavior and exits 0.

**Steps:**

- [ ] **Step 1: Replace the example program**

Overwrite `examples/JoakimAnder.Toolbox.Examples/Program.cs`:

```csharp
using System.Diagnostics;
using JoakimAnder.Toolbox.Threading;

Console.WriteLine("ParallelFanout example: fail-fast with sibling cancellation\n");

var sw = Stopwatch.StartNew();
try
{
    var (user, orders) = await new FanOut()
        .Add(ct => GetUserAsync(ct))
        .Add(ct => GetOrdersAsync(ct))
        .Add(ct => AuditAsync(ct))
        .WhenAll();

    Console.WriteLine($"Got {user} with {orders.Length} orders.");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"\nFan-out failed after {sw.ElapsedMilliseconds} ms: {ex.Message}");
    Console.WriteLine("(The 30s order fetch was cancelled instead of running to completion.)");
}

static async Task<string> GetUserAsync(CancellationToken ct)
{
    await Task.Delay(100, ct);
    throw new InvalidOperationException("user service unavailable");
}

static async Task<int[]> GetOrdersAsync(CancellationToken ct)
{
    try
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("orders fetch: observed cancellation, bailing out");
        throw;
    }

    return [1, 2, 3];
}

static async Task AuditAsync(CancellationToken ct)
{
    await Task.Delay(50, ct);
    Console.WriteLine("audit: recorded");
}
```

- [ ] **Step 2: Run the example**

Run: `dotnet run --project examples/JoakimAnder.Toolbox.Examples`
Expected output (timing ~100–200 ms, ordering of the first two lines may vary):
```
ParallelFanout example: fail-fast with sibling cancellation

audit: recorded
orders fetch: observed cancellation, bailing out

Fan-out failed after 1XX ms: user service unavailable
(The 30s order fetch was cancelled instead of running to completion.)
```

- [ ] **Step 3: Update the README**

In `README.md`, replace the "What's in the box" section (the heading line through the line ending `(Result, ParallelFanout, DI source generator).`) with:

````markdown
## What's in the box

### ParallelFanout — `JoakimAnder.Toolbox.Threading.FanOut`

Run several async operations that must *all* succeed, and cancel the rest the instant one
fails — so a long-running sibling isn't left burning time and resources after the outcome
is already decided.

```csharp
using JoakimAnder.Toolbox.Threading;

var (user, orders) = await new FanOut()
    .Add(ct => GetUserAsync(ct))
    .Add(ct => GetOrdersAsync(ct))
    .WhenAll(cancellationToken);
```

If `GetUserAsync` throws, the token handed to `GetOrdersAsync` is cancelled and the original
exception is rethrown unwrapped. Operations are factories (`ct => …`) so the token can be
threaded into each one. A static `FanOut.WhenAll(...)` is also available for quick one-liners
and `void` operations.

See [docs/ROADMAP.md](docs/ROADMAP.md) for the rest of the planned features (Result, DI source generator).
````

- [ ] **Step 4: Update the CHANGELOG**

In `CHANGELOG.md`, replace the `## [Unreleased]` line with:

```markdown
## [Unreleased]

### Added

- `ParallelFanout` (`JoakimAnder.Toolbox.Threading.FanOut`): fail-fast parallel execution of async operations that must all succeed, cancelling the remaining operations on the first fault. Fluent typed builder (`new FanOut().Add(...).Add(...).WhenAll(ct)`) plus static `WhenAll` overloads — void and typed, arity 2–8, and an `IEnumerable` void overload.
```

- [ ] **Step 5: Final verification**

Run: `dotnet build -c Release` → 0 warnings.
Run: `dotnet test` → all pass.
Run: `dotnet run --project examples/JoakimAnder.Toolbox.Examples` → prints the cancellation behavior.

- [ ] **Step 6: Commit**

```bash
git add examples/JoakimAnder.Toolbox.Examples/Program.cs README.md CHANGELOG.md
git commit -m "docs(threading): add FanOut example and document the feature"
```

---

## Notes for the implementer

- **Test through the public API only.** The engine is `internal`; every behavior is exercised via `FanOut` / `FanOut.WhenAll`. No `InternalsVisibleTo` is needed.
- **Overload resolution:** a lambda returning `Task<T>` binds to `Add<T>` (typed); a lambda returning a bare `Task` binds to `Add` (void). This is intentional — keep void ops returning `Task` (e.g. `=> Task.CompletedTask`).
- **Zero warnings is a gate.** The repo runs `AnalysisMode=Recommended` with `EnforceCodeStyleInBuild`. If any analyzer or style warning appears, fix it before committing (file-scoped namespaces, braces, `var` usage already match the `.editorconfig`).
- **Timing tests** use generous bounds (sibling delay 30s, assert return < 5s) to stay non-flaky on CI.
