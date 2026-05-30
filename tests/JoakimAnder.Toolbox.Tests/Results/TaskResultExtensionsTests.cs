using JoakimAnder.Toolbox.Results;
using Xunit;

namespace JoakimAnder.Toolbox.Tests.Results;

public class TaskResultExtensionsTests
{
    private sealed record Err(string Code, string Message);
    private sealed record MappedErr(string Tag);

    private static Task<Result<int, Err>> OkAsync(int value) =>
        Task.FromResult(Result<int, Err>.Success(value));

    private static Task<Result<int, Err>> FailAsync(string code) =>
        Task.FromResult(Result<int, Err>.Failure(new Err(code, "x")));

    private static Task<Result<Err>> VoidOkAsync() =>
        Task.FromResult(Result<Err>.Success());

    private static Task<Result<Err>> VoidFailAsync(string code) =>
        Task.FromResult(Result<Err>.Failure(new Err(code, "x")));

    // --- Map / MapError on Task<Result<T, TError>> ---

    [Fact]
    public async Task Map_async_applies_function_on_success()
    {
        var r = await OkAsync(3).Map(v => v * 2);
        Assert.True(r.TryGetValue(out var v, out _));
        Assert.Equal(6, v);
    }

    [Fact]
    public async Task Map_async_propagates_failure()
    {
        var r = await FailAsync("X").Map(v => v * 2);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public async Task MapError_async_applies_on_failure()
    {
        var r = await FailAsync("X").MapError(e => new MappedErr(e.Code));
        Assert.True(r.TryGetError(out var e2));
        Assert.Equal("X", e2.Tag);
    }

    // --- Bind / BindAsync on Task<Result<T, TError>> ---

    [Fact]
    public async Task Bind_async_chains_with_sync_next()
    {
        var r = await OkAsync(3).Bind(v => Result<string, Err>.Success($"v={v}"));
        Assert.True(r.TryGetValue(out var s, out _));
        Assert.Equal("v=3", s);
    }

    [Fact]
    public async Task BindAsync_chains_with_async_next()
    {
        var r = await OkAsync(3)
            .BindAsync(v => Task.FromResult(Result<string, Err>.Success($"v={v}")));
        Assert.True(r.TryGetValue(out var s, out _));
        Assert.Equal("v=3", s);
    }

    [Fact]
    public async Task BindAsync_propagates_failure_without_invoking_next()
    {
        var invoked = false;
        var r = await FailAsync("X").BindAsync<int, string, Err>(_ =>
        {
            invoked = true;
            return Task.FromResult(Result<string, Err>.Success("ok"));
        });

        Assert.False(invoked);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public async Task Multi_step_pipeline_mixes_sync_and_async_binds()
    {
        var r = await OkAsync(2)                                                       // 2
            .BindAsync(v => Task.FromResult(Result<int, Err>.Success(v * 10)))         // 20
            .Bind(v => Result<int, Err>.Success(v + 1))                                // 21
            .Map(v => $"final={v}");                                                   // "final=21"

        Assert.True(r.TryGetValue(out var s, out _));
        Assert.Equal("final=21", s);
    }

    // --- Match / MatchAsync on Task<Result<T, TError>> ---

    [Fact]
    public async Task Match_async_returns_onSuccess_result()
    {
        var got = await OkAsync(5).Match(v => v * 2, _ => -1);
        Assert.Equal(10, got);
    }

    [Fact]
    public async Task MatchAsync_runs_async_branches()
    {
        var got = await OkAsync(5)
            .MatchAsync(v => Task.FromResult(v * 3), _ => Task.FromResult(-1));
        Assert.Equal(15, got);
    }

    // --- ValueOrThrowAsync ---

    [Fact]
    public async Task ValueOrThrowAsync_on_success_returns_value()
    {
        var v = await OkAsync(7).ValueOrThrowAsync();
        Assert.Equal(7, v);
    }

    [Fact]
    public async Task ValueOrThrowAsync_on_failure_throws_default_exception()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FailAsync("X").ValueOrThrowAsync());
    }

    [Fact]
    public async Task ValueOrThrowAsync_with_mapper_throws_mapped_exception()
    {
        await Assert.ThrowsAsync<DivideByZeroException>(
            () => FailAsync("X").ValueOrThrowAsync(_ => new DivideByZeroException()));
    }

    // --- Result<TError> async extensions ---

    [Fact]
    public async Task Void_MapError_async_applies_on_failure()
    {
        var r = await VoidFailAsync("X").MapError(e => new MappedErr(e.Code));
        Assert.True(r.TryGetError(out var e2));
        Assert.Equal("X", e2.Tag);
    }

    [Fact]
    public async Task Void_BindAsync_to_void_chains_on_success()
    {
        var invoked = false;
        var r = await VoidOkAsync().BindAsync(() =>
        {
            invoked = true;
            return Task.FromResult(Result<Err>.Success());
        });

        Assert.True(invoked);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task Void_BindAsync_to_typed_chains_on_success()
    {
        var r = await VoidOkAsync()
            .BindAsync(() => Task.FromResult(Result<int, Err>.Success(42)));

        Assert.True(r.TryGetValue(out var v, out _));
        Assert.Equal(42, v);
    }

    [Fact]
    public async Task Void_BindAsync_to_void_propagates_failure_without_invoking_next()
    {
        var invoked = false;
        var r = await VoidFailAsync("X").BindAsync(() =>
        {
            invoked = true;
            return Task.FromResult(Result<Err>.Success());
        });

        Assert.False(invoked);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public async Task Void_BindAsync_to_typed_propagates_failure_without_invoking_next()
    {
        var invoked = false;
        var r = await VoidFailAsync("X").BindAsync<int, Err>(() =>
        {
            invoked = true;
            return Task.FromResult(Result<int, Err>.Success(1));
        });

        Assert.False(invoked);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public async Task Void_Bind_to_void_propagates_failure_without_invoking_next()
    {
        var invoked = false;
        var r = await VoidFailAsync("X").Bind(() => { invoked = true; return Result<Err>.Success(); });

        Assert.False(invoked);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public async Task Void_Bind_to_typed_propagates_failure()
    {
        var r = await VoidFailAsync("X").Bind(() => Result<int, Err>.Success(42));
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public async Task Void_Match_async_returns_onSuccess_result()
    {
        var got = await VoidOkAsync().Match(() => "ok", _ => "bad");
        Assert.Equal("ok", got);
    }

    [Fact]
    public async Task Void_MatchAsync_runs_onSuccess_branch()
    {
        var got = await VoidOkAsync()
            .MatchAsync(() => Task.FromResult("ok"), _ => Task.FromResult("bad"));
        Assert.Equal("ok", got);
    }

    [Fact]
    public async Task Void_MatchAsync_runs_onFailure_branch()
    {
        var got = await VoidFailAsync("X")
            .MatchAsync(() => Task.FromResult("ok"), e => Task.FromResult(e.Code));
        Assert.Equal("X", got);
    }

    [Fact]
    public async Task ThrowIfFailureAsync_on_success_returns_silently()
    {
        await VoidOkAsync().ThrowIfFailureAsync();
    }

    [Fact]
    public async Task ThrowIfFailureAsync_with_mapper_on_success_returns_silently_without_invoking_mapper()
    {
        var invoked = false;
        await VoidOkAsync().ThrowIfFailureAsync(_ => { invoked = true; return new InvalidOperationException("never"); });
        Assert.False(invoked);
    }

    [Fact]
    public async Task ThrowIfFailureAsync_on_failure_throws_default_exception()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => VoidFailAsync("X").ThrowIfFailureAsync());
    }

    [Fact]
    public async Task ThrowIfFailureAsync_with_mapper_throws_mapped_exception()
    {
        await Assert.ThrowsAsync<DivideByZeroException>(
            () => VoidFailAsync("X").ThrowIfFailureAsync(_ => new DivideByZeroException()));
    }

    // --- Null-argument guards ---

    [Fact]
    public void Null_task_throws_ArgumentNullException_synchronously()
    {
        Task<Result<int, Err>> nullTask = null!;

        // Each extension is async but ThrowIfNull runs before the first await,
        // so the exception surfaces synchronously at the call site.
        // Assign to discard inside an Action lambda so xUnit sees Action, not Func<Task>.
        Assert.Throws<ArgumentNullException>((Action)(() => { _ = nullTask.Map(v => v); }));
        Assert.Throws<ArgumentNullException>((Action)(() => { _ = nullTask.MapError(e => e); }));
        Assert.Throws<ArgumentNullException>((Action)(() => { _ = nullTask.Bind(v => Result<int, Err>.Success(v)); }));
        Assert.Throws<ArgumentNullException>((Action)(() => { _ = nullTask.BindAsync(v => Task.FromResult(Result<int, Err>.Success(v))); }));
    }
}
