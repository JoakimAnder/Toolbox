using JoakimAnder.Toolbox.Results;
using Xunit;

namespace JoakimAnder.Toolbox.Tests.Results;

public class ResultOperatorsTests
{
    private sealed record Err(string Code, string Message);
    private sealed record MappedErr(string Tag);

    // --- Result<T, TError>.Map ---

    [Fact]
    public void Map_on_success_applies_function()
    {
        var r = Result<int, Err>.Success(3).Map(v => v * 2);
        Assert.True(r.IsSuccess);
        Assert.True(r.TryGetValue(out var v, out _));
        Assert.Equal(6, v);
    }

    [Fact]
    public void Map_on_failure_propagates_error_without_invoking_function()
    {
        var invoked = false;
        var r = Result<int, Err>.Failure(new Err("X", "x")).Map(v => { invoked = true; return v * 2; });
        Assert.False(invoked);
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public void Map_on_default_throws_uninitialized()
    {
        var r = default(Result<int, Err>);
        Assert.Throws<InvalidOperationException>(() => r.Map(v => v * 2));
    }

    [Fact]
    public void Map_with_null_delegate_throws_ArgumentNullException()
    {
        var r = Result<int, Err>.Success(1);
        Assert.Throws<ArgumentNullException>(() => r.Map<int>(null!));
    }

    [Fact]
    public void Map_propagates_exception_from_lambda()
    {
        var r = Result<int, Err>.Success(1);
        Assert.Throws<DivideByZeroException>(() => r.Map<int>(_ => throw new DivideByZeroException()));
    }

    // --- Result<T, TError>.MapError ---

    [Fact]
    public void MapError_on_failure_applies_function()
    {
        var r = Result<int, Err>.Failure(new Err("X", "x")).MapError(e => new MappedErr(e.Code));
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e2));
        Assert.Equal("X", e2.Tag);
    }

    [Fact]
    public void MapError_on_success_propagates_value()
    {
        var r = Result<int, Err>.Success(7).MapError(e => new MappedErr(e.Code));
        Assert.True(r.IsSuccess);
        Assert.True(r.TryGetValue(out var v, out _));
        Assert.Equal(7, v);
    }

    [Fact]
    public void MapError_on_default_throws_uninitialized()
    {
        var r = default(Result<int, Err>);
        Assert.Throws<InvalidOperationException>(() => r.MapError(e => new MappedErr(e.Code)));
    }

    // --- Result<T, TError>.Bind<U> ---

    [Fact]
    public void Bind_on_success_invokes_next_and_returns_its_result()
    {
        var r = Result<int, Err>.Success(3).Bind(v => Result<string, Err>.Success($"v={v}"));
        Assert.True(r.IsSuccess);
        Assert.True(r.TryGetValue(out var s, out _));
        Assert.Equal("v=3", s);
    }

    [Fact]
    public void Bind_on_success_propagates_next_failure()
    {
        var r = Result<int, Err>.Success(3)
            .Bind<string>(_ => Result<string, Err>.Failure(new Err("X", "x")));
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public void Bind_on_failure_propagates_error_without_invoking_next()
    {
        var invoked = false;
        var r = Result<int, Err>.Failure(new Err("X", "x"))
            .Bind(_ => { invoked = true; return Result<string, Err>.Success("ok"); });
        Assert.False(invoked);
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public void Bind_on_default_throws_uninitialized()
    {
        var r = default(Result<int, Err>);
        Assert.Throws<InvalidOperationException>(
            () => r.Bind(v => Result<string, Err>.Success(v.ToString(System.Globalization.CultureInfo.InvariantCulture))));
    }

    // --- Result<T, TError>.Bind to Result<TError> ---

    [Fact]
    public void Bind_to_void_on_success_invokes_next()
    {
        var r = Result<int, Err>.Success(3).Bind(_ => Result<Err>.Success());
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Bind_to_void_on_failure_propagates_error()
    {
        var r = Result<int, Err>.Failure(new Err("X", "x")).Bind(_ => Result<Err>.Success());
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    // --- Result<TError>.MapError ---

    [Fact]
    public void Void_MapError_on_failure_applies_function()
    {
        var r = Result<Err>.Failure(new Err("X", "x")).MapError(e => new MappedErr(e.Code));
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e2));
        Assert.Equal("X", e2.Tag);
    }

    [Fact]
    public void Void_MapError_on_success_stays_success()
    {
        var r = Result<Err>.Success().MapError(e => new MappedErr(e.Code));
        Assert.True(r.IsSuccess);
    }

    // --- Result<TError>.Bind ---

    [Fact]
    public void Void_Bind_to_void_on_success_invokes_next()
    {
        var invoked = false;
        var r = Result<Err>.Success().Bind(() => { invoked = true; return Result<Err>.Success(); });
        Assert.True(invoked);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Void_Bind_to_void_on_failure_skips_next()
    {
        var invoked = false;
        var r = Result<Err>.Failure(new Err("X", "x"))
            .Bind(() => { invoked = true; return Result<Err>.Success(); });
        Assert.False(invoked);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void Void_Bind_to_typed_on_success_invokes_next()
    {
        var r = Result<Err>.Success().Bind(() => Result<int, Err>.Success(42));
        Assert.True(r.IsSuccess);
        Assert.True(r.TryGetValue(out var v, out _));
        Assert.Equal(42, v);
    }

    [Fact]
    public void Void_Bind_to_typed_on_failure_propagates_error()
    {
        var r = Result<Err>.Failure(new Err("X", "x")).Bind(() => Result<int, Err>.Success(42));
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    // --- Null-delegate guards ---

    [Fact]
    public void All_operators_throw_ArgumentNullException_on_null_delegate()
    {
        var t = Result<int, Err>.Success(1);
        Assert.Throws<ArgumentNullException>(() => t.Map<int>(null!));
        Assert.Throws<ArgumentNullException>(() => t.MapError<MappedErr>(null!));
        Assert.Throws<ArgumentNullException>(() => t.Bind<int>((Func<int, Result<int, Err>>)null!));
        Assert.Throws<ArgumentNullException>(() => t.Bind((Func<int, Result<Err>>)null!));

        var v = Result<Err>.Success();
        Assert.Throws<ArgumentNullException>(() => v.MapError<MappedErr>(null!));
        Assert.Throws<ArgumentNullException>(() => v.Bind((Func<Result<Err>>)null!));
        Assert.Throws<ArgumentNullException>(() => v.Bind<int>((Func<Result<int, Err>>)null!));
    }

    // --- End-to-end pipeline ---

    [Fact]
    public void Chained_pipeline_short_circuits_on_first_failure()
    {
        var step3Called = false;

        var r = Result<int, Err>.Success(2)
            .Map(v => v * 10)                       // 20
            .Bind<int>(_ => Result<int, Err>.Failure(new Err("X", "step2"))) // <- fails here
            .Map(v => { step3Called = true; return v + 1; });

        Assert.True(r.IsFailure);
        Assert.False(step3Called);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }
}
