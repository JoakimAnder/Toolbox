using JoakimAnder.Toolbox.Results;
using Xunit;

namespace JoakimAnder.Toolbox.Tests.Results;

public class ResultOfTErrorTests
{
    private sealed record Err(string Code, string Message);

    // --- Factories + state inspection ---

    [Fact]
    public void Success_factory_yields_success_state()
    {
        var r = Result<Err>.Success();
        Assert.True(r.IsSuccess);
        Assert.False(r.IsFailure);
        Assert.False(r.IsDefault);
    }

    [Fact]
    public void Failure_factory_yields_failure_state()
    {
        var r = Result<Err>.Failure(new Err("X", "boom"));
        Assert.False(r.IsSuccess);
        Assert.True(r.IsFailure);
        Assert.False(r.IsDefault);
    }

    [Fact]
    public void Failure_with_null_error_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result<Err>.Failure(null!));
    }

    [Fact]
    public void Default_value_is_in_uninitialized_state()
    {
        var r = default(Result<Err>);
        Assert.False(r.IsSuccess);
        Assert.False(r.IsFailure);
        Assert.True(r.IsDefault);
    }

    // --- Implicit conversions ---

    [Fact]
    public void Implicit_from_error_yields_failure()
    {
        Result<Err> r = new Err("X", "x");
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public void Implicit_from_Failure_carrier_yields_failure()
    {
        Result<Err> r = Result.Failure(new Err("Z", "z"));
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("Z", e.Code);
    }

    // --- TryGetError ---

    [Fact]
    public void TryGetError_on_failure_returns_true_with_error()
    {
        var r = Result<Err>.Failure(new Err("Y", "y"));
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("Y", e.Code);
    }

    [Fact]
    public void TryGetError_on_success_returns_false_with_default_error()
    {
        var r = Result<Err>.Success();
        Assert.False(r.TryGetError(out var e));
        Assert.Null(e);
    }

    [Fact]
    public void TryGetError_NRT_flow_analysis_pattern_compiles_and_runs()
    {
        var r = Result<Err>.Failure(new Err("X", "x"));
        if (r.TryGetError(out var error))
        {
            // error is non-null in this branch (analyzer fact via [NotNullWhen(true)]).
            Assert.Equal("X", error.Code);
        }
        else
        {
            Assert.Fail("expected failure branch");
        }
    }

    // --- Match ---

    [Fact]
    public void Match_void_invokes_onSuccess()
    {
        var r = Result<Err>.Success();
        var hit = false;
        r.Match(onSuccess: () => hit = true, onFailure: _ => hit = false);
        Assert.True(hit);
    }

    [Fact]
    public void Match_void_invokes_onFailure_with_error()
    {
        var r = Result<Err>.Failure(new Err("X", "x"));
        string? code = null;
        r.Match(onSuccess: () => code = "wrong", onFailure: e => code = e.Code);
        Assert.Equal("X", code);
    }

    [Fact]
    public void Match_returning_returns_onSuccess_result()
    {
        var r = Result<Err>.Success();
        var got = r.Match(onSuccess: () => 1, onFailure: _ => 0);
        Assert.Equal(1, got);
    }

    [Fact]
    public void Match_returning_returns_onFailure_result()
    {
        var r = Result<Err>.Failure(new Err("X", "x"));
        var got = r.Match(onSuccess: () => "ok", onFailure: e => e.Code);
        Assert.Equal("X", got);
    }

    [Fact]
    public void Match_with_null_delegates_throws_ArgumentNullException()
    {
        var r = Result<Err>.Success();
        Assert.Throws<ArgumentNullException>(() => r.Match(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => r.Match(() => { }, null!));
        Assert.Throws<ArgumentNullException>(() => r.Match<int>(null!, _ => 0));
        Assert.Throws<ArgumentNullException>(() => r.Match<int>(() => 0, null!));
    }

    // --- ThrowIfFailure ---

    [Fact]
    public void ThrowIfFailure_on_success_returns_silently()
    {
        var r = Result<Err>.Success();
        r.ThrowIfFailure();
    }

    [Fact]
    public void ThrowIfFailure_with_mapper_on_success_returns_silently_without_invoking_mapper()
    {
        var r = Result<Err>.Success();
        var mapperInvoked = false;
        r.ThrowIfFailure(_ => { mapperInvoked = true; return new InvalidOperationException("never"); });
        Assert.False(mapperInvoked);
    }

    [Fact]
    public void ThrowIfFailure_on_failure_throws_InvalidOperationException()
    {
        var r = Result<Err>.Failure(new Err("X", "boom"));
        var ex = Assert.Throws<InvalidOperationException>(() => r.ThrowIfFailure());
        Assert.Contains("X", ex.Message);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public void ThrowIfFailure_with_mapper_throws_mapped_exception()
    {
        var r = Result<Err>.Failure(new Err("X", "boom"));
        var ex = Assert.Throws<InvalidOperationException>(
            () => r.ThrowIfFailure(e => new InvalidOperationException($"mapped:{e.Code}")));
        Assert.Equal("mapped:X", ex.Message);
    }

    [Fact]
    public void ThrowIfFailure_with_mapper_returning_null_throws_InvalidOperationException()
    {
        var r = Result<Err>.Failure(new Err("X", "boom"));
        var ex = Assert.Throws<InvalidOperationException>(() => r.ThrowIfFailure(_ => null!));
        Assert.Contains("Mapped exception was null", ex.Message);
    }

    [Fact]
    public void ThrowIfFailure_with_null_mapper_throws_ArgumentNullException()
    {
        var r = Result<Err>.Failure(new Err("X", "x"));
        Assert.Throws<ArgumentNullException>(
            () => r.ThrowIfFailure((Func<Err, Exception>)null!));
    }

    // --- Default-state guard ---

    [Fact]
    public void Default_TryGetError_throws_uninitialized()
    {
        var r = default(Result<Err>);
        var ex = Assert.Throws<InvalidOperationException>(() => r.TryGetError(out _));
        Assert.Equal("Result is uninitialized.", ex.Message);
    }

    [Fact]
    public void Default_Match_throws_uninitialized()
    {
        var r = default(Result<Err>);
        Assert.Throws<InvalidOperationException>(() => r.Match(() => { }, _ => { }));
        Assert.Throws<InvalidOperationException>(() => r.Match(() => 0, _ => 0));
    }

    [Fact]
    public void Default_ThrowIfFailure_throws_uninitialized()
    {
        var r = default(Result<Err>);
        Assert.Throws<InvalidOperationException>(() => r.ThrowIfFailure());
        Assert.Throws<InvalidOperationException>(() => r.ThrowIfFailure(_ => new InvalidOperationException()));
    }
}
