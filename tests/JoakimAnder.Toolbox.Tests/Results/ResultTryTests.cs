using JoakimAnder.Toolbox.Results;
using Xunit;

namespace JoakimAnder.Toolbox.Tests.Results;

public class ResultTryTests
{
    private sealed record Err(string Code, string Message);

    // --- Sync typed ---

    [Fact]
    public void Try_typed_returns_success_when_action_succeeds()
    {
        var r = Result.Try(() => 42, ex => new Err("X", ex.Message));
        Assert.True(r.IsSuccess);
        Assert.True(r.TryGetValue(out var v, out _));
        Assert.Equal(42, v);
    }

    [Fact]
    public void Try_typed_returns_failure_when_action_throws_non_OCE()
    {
        var r = Result.Try<int, Err>(
            () => throw new InvalidOperationException("boom"),
            ex => new Err("MAPPED", ex.Message));

        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("MAPPED", e.Code);
        Assert.Equal("boom", e.Message);
    }

    [Fact]
    public void Try_typed_rethrows_OperationCanceledException_unchanged()
    {
        var ex = Assert.Throws<OperationCanceledException>(() =>
            Result.Try<int, Err>(
                () => throw new OperationCanceledException("cancelled"),
                e => new Err("WRONG", e.Message)));

        Assert.Equal("cancelled", ex.Message);
    }

    [Fact]
    public void Try_typed_rethrows_TaskCanceledException_unchanged()
    {
        Assert.Throws<TaskCanceledException>(() =>
            Result.Try<int, Err>(
                () => throw new TaskCanceledException("cancelled-task"),
                e => new Err("WRONG", e.Message)));
    }

    [Fact]
    public void Try_typed_propagates_onException_throw()
    {
        Assert.Throws<DivideByZeroException>(() =>
            Result.Try<int, Err>(
                () => throw new InvalidOperationException("inner"),
                _ => throw new DivideByZeroException()));
    }

    // --- Sync void ---

    [Fact]
    public void Try_void_returns_success_when_action_succeeds()
    {
        var ran = false;
        var r = Result.Try(() => { ran = true; }, ex => new Err("X", ex.Message));
        Assert.True(ran);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Try_void_returns_failure_when_action_throws_non_OCE()
    {
        var r = Result.Try<Err>(
            () => throw new InvalidOperationException("boom"),
            ex => new Err("MAPPED", ex.Message));

        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("MAPPED", e.Code);
    }

    [Fact]
    public void Try_void_rethrows_OCE_unchanged()
    {
        Assert.Throws<OperationCanceledException>(() =>
            Result.Try<Err>(
                () => throw new OperationCanceledException(),
                e => new Err("WRONG", e.Message)));
    }

    // --- Async typed ---

    [Fact]
    public async Task TryAsync_typed_returns_success_when_action_succeeds()
    {
        var r = await Result.TryAsync(
            ct => Task.FromResult(42),
            ex => new Err("X", ex.Message));

        Assert.True(r.IsSuccess);
        Assert.True(r.TryGetValue(out var v, out _));
        Assert.Equal(42, v);
    }

    [Fact]
    public async Task TryAsync_typed_returns_failure_when_action_throws_non_OCE()
    {
        var r = await Result.TryAsync<int, Err>(
            ct => Task.FromException<int>(new InvalidOperationException("boom")),
            ex => new Err("MAPPED", ex.Message));

        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("MAPPED", e.Code);
    }

    [Fact]
    public async Task TryAsync_typed_rethrows_OperationCanceledException_unchanged()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Result.TryAsync<int, Err>(
                async ct => { await Task.Delay(TimeSpan.FromSeconds(30), ct); return 1; },
                ex => new Err("WRONG", ex.Message),
                cts.Token));
    }

    [Fact]
    public async Task TryAsync_typed_threads_cancellation_token_into_action()
    {
        CancellationToken seen = default;
        using var cts = new CancellationTokenSource();

        await Result.TryAsync(
            ct => { seen = ct; return Task.FromResult(0); },
            ex => new Err("X", ex.Message),
            cts.Token);

        // The action's token is the *linked* token, but should reflect the outer cts.
        // We at least verify that the action received a usable token and that the
        // outer token's cancellation reaches it.
        Assert.False(seen.IsCancellationRequested);
        cts.Cancel();
        Assert.True(seen.IsCancellationRequested);
    }

    // --- Async void ---

    [Fact]
    public async Task TryAsync_void_returns_success_when_action_succeeds()
    {
        var ran = false;
        var r = await Result.TryAsync<Err>(
            ct => { ran = true; return Task.CompletedTask; },
            ex => new Err("X", ex.Message));

        Assert.True(ran);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task TryAsync_void_returns_failure_when_action_throws_non_OCE()
    {
        var r = await Result.TryAsync<Err>(
            ct => Task.FromException(new InvalidOperationException("boom")),
            ex => new Err("MAPPED", ex.Message));

        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("MAPPED", e.Code);
    }

    [Fact]
    public async Task TryAsync_void_rethrows_OCE_unchanged()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Result.TryAsync<Err>(
                async ct => await Task.Delay(TimeSpan.FromSeconds(30), ct),
                ex => new Err("WRONG", ex.Message),
                cts.Token));
    }

    // --- Null-argument guards ---

    [Fact]
    public void Try_with_null_arguments_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => Result.Try<int, Err>(null!, ex => new Err("X", "")));
        Assert.Throws<ArgumentNullException>(
            () => Result.Try<int, Err>(() => 1, null!));
        Assert.Throws<ArgumentNullException>(
            () => Result.Try<Err>(null!, ex => new Err("X", "")));
        Assert.Throws<ArgumentNullException>(
            () => Result.Try<Err>(() => { }, null!));
    }

    [Fact]
    public async Task TryAsync_with_null_arguments_throws_ArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync<int, Err>(null!, ex => new Err("X", "")));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync<int, Err>(ct => Task.FromResult(1), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync<Err>(null!, ex => new Err("X", "")));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync<Err>(ct => Task.CompletedTask, null!));
    }
}
