using JoakimAnder.Toolbox.Results;
using Xunit;

namespace JoakimAnder.Toolbox.Tests.Results;

public class ResultOfTAndTErrorTests
{
    private sealed record Err(string Code, string Message);

    // --- Factories + state inspection ---

    [Fact]
    public void Success_factory_yields_success_state()
    {
        var r = Result<int, Err>.Success(42);
        Assert.True(r.IsSuccess);
        Assert.False(r.IsFailure);
        Assert.False(r.IsDefault);
    }

    [Fact]
    public void Success_with_null_value_for_nullable_T_round_trips()
    {
        var r = Result<string?, Err>.Success(null);

        Assert.True(r.IsSuccess);
        Assert.True(r.TryGetValue(out var value, out _));
        Assert.Null(value);
        Assert.Null(r.ValueOrThrow());
    }

    [Fact]
    public void Failure_factory_yields_failure_state()
    {
        var r = Result<int, Err>.Failure(new Err("X", "boom"));
        Assert.False(r.IsSuccess);
        Assert.True(r.IsFailure);
        Assert.False(r.IsDefault);
    }

    [Fact]
    public void Failure_with_null_error_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result<int, Err>.Failure(null!));
    }

    [Fact]
    public void Default_value_is_in_uninitialized_state()
    {
        var r = default(Result<int, Err>);
        Assert.False(r.IsSuccess);
        Assert.False(r.IsFailure);
        Assert.True(r.IsDefault);
    }

    // --- Implicit conversions ---

    [Fact]
    public void Implicit_from_value_yields_success()
    {
        Result<int, Err> r = 7;
        Assert.True(r.IsSuccess);
        Assert.True(r.TryGetValue(out var v, out _));
        Assert.Equal(7, v);
    }

    [Fact]
    public void Implicit_from_error_yields_failure()
    {
        Result<int, Err> r = new Err("X", "no");
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public void Implicit_from_Success_carrier_yields_success()
    {
        Result<int, Err> r = Result.Success(11);
        Assert.True(r.IsSuccess);
        Assert.True(r.TryGetValue(out var v, out _));
        Assert.Equal(11, v);
    }

    [Fact]
    public void Implicit_from_Failure_carrier_yields_failure()
    {
        Result<int, Err> r = Result.Failure(new Err("Z", "z"));
        Assert.True(r.IsFailure);
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("Z", e.Code);
    }

    [Fact]
    public void Carrier_helpers_disambiguate_when_T_equals_TError()
    {
        // Bare implicit conversion would be ambiguous here — both `T = string`
        // and `TError = string` apply. Carriers route through distinct types.
        Result<string, string> ok = Result.Success("ok");
        Result<string, string> bad = Result.Failure("bad");

        Assert.True(ok.IsSuccess);
        Assert.True(ok.TryGetValue(out var v, out _));
        Assert.Equal("ok", v);

        Assert.True(bad.IsFailure);
        Assert.True(bad.TryGetError(out var e));
        Assert.Equal("bad", e);
    }

    // --- TryGetValue / TryGetError ---

    [Fact]
    public void TryGetValue_on_success_returns_true_with_value_and_default_error()
    {
        var r = Result<int, Err>.Success(3);
        Assert.True(r.TryGetValue(out var v, out var e));
        Assert.Equal(3, v);
        Assert.Null(e);
    }

    [Fact]
    public void TryGetValue_on_failure_returns_false_with_error_and_default_value()
    {
        var r = Result<int, Err>.Failure(new Err("X", "no"));
        Assert.False(r.TryGetValue(out var v, out var e));
        Assert.Equal(0, v);
        Assert.NotNull(e);
        Assert.Equal("X", e.Code);
    }

    [Fact]
    public void TryGetError_on_failure_returns_true_with_error()
    {
        var r = Result<int, Err>.Failure(new Err("Y", "y"));
        Assert.True(r.TryGetError(out var e));
        Assert.Equal("Y", e.Code);
    }

    [Fact]
    public void TryGetError_on_success_returns_false_with_default_error()
    {
        var r = Result<int, Err>.Success(0);
        Assert.False(r.TryGetError(out var e));
        Assert.Null(e);
    }

    // --- Match ---

    [Fact]
    public void Match_void_invokes_onSuccess_with_value()
    {
        var r = Result<int, Err>.Success(5);
        var hit = -1;
        r.Match(onSuccess: v => hit = v, onFailure: _ => hit = -999);
        Assert.Equal(5, hit);
    }

    [Fact]
    public void Match_void_invokes_onFailure_with_error()
    {
        var r = Result<int, Err>.Failure(new Err("X", "x"));
        string? hit = null;
        r.Match(onSuccess: _ => hit = "wrong", onFailure: e => hit = e.Code);
        Assert.Equal("X", hit);
    }

    [Fact]
    public void Match_returning_returns_onSuccess_result()
    {
        var r = Result<int, Err>.Success(10);
        var got = r.Match(onSuccess: v => v * 2, onFailure: _ => -1);
        Assert.Equal(20, got);
    }

    [Fact]
    public void Match_returning_returns_onFailure_result()
    {
        var r = Result<int, Err>.Failure(new Err("X", "x"));
        var got = r.Match(onSuccess: _ => "ok", onFailure: e => e.Code);
        Assert.Equal("X", got);
    }

    [Fact]
    public void Match_with_null_delegates_throws_ArgumentNullException()
    {
        var r = Result<int, Err>.Success(1);
        Assert.Throws<ArgumentNullException>(() => r.Match(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => r.Match(_ => { }, null!));
        Assert.Throws<ArgumentNullException>(() => r.Match<int>(null!, _ => 0));
        Assert.Throws<ArgumentNullException>(() => r.Match<int>(_ => 0, null!));
    }

    // --- ValueOrThrow ---

    [Fact]
    public void ValueOrThrow_on_success_returns_value()
    {
        var r = Result<int, Err>.Success(99);
        Assert.Equal(99, r.ValueOrThrow());
    }

    [Fact]
    public void ValueOrThrow_with_mapper_on_success_returns_value_without_invoking_mapper()
    {
        var mapperInvoked = false;
        var r = Result<int, Err>.Success(99);

        var got = r.ValueOrThrow(_ => { mapperInvoked = true; return new InvalidOperationException("never"); });

        Assert.Equal(99, got);
        Assert.False(mapperInvoked);
    }

    [Fact]
    public void ValueOrThrow_on_failure_throws_InvalidOperationException_with_error_in_message()
    {
        var r = Result<int, Err>.Failure(new Err("X", "boom"));
        var ex = Assert.Throws<InvalidOperationException>(() => r.ValueOrThrow());
        Assert.Contains("X", ex.Message);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public void ValueOrThrow_with_mapper_throws_mapped_exception()
    {
        var r = Result<int, Err>.Failure(new Err("X", "boom"));
        var ex = Assert.Throws<InvalidOperationException>(
            () => r.ValueOrThrow(e => new InvalidOperationException($"mapped:{e.Code}")));
        Assert.Equal("mapped:X", ex.Message);
    }

    [Fact]
    public void ValueOrThrow_with_mapper_returning_null_throws_InvalidOperationException()
    {
        var r = Result<int, Err>.Failure(new Err("X", "boom"));
        var ex = Assert.Throws<InvalidOperationException>(() => r.ValueOrThrow(_ => null!));
        Assert.Contains("Mapped exception was null", ex.Message);
    }

    [Fact]
    public void ValueOrThrow_with_null_mapper_throws_ArgumentNullException()
    {
        var r = Result<int, Err>.Failure(new Err("X", "x"));
        Assert.Throws<ArgumentNullException>(
            () => r.ValueOrThrow((Func<Err, Exception>)null!));
    }

    // --- Default-state guard ---

    [Fact]
    public void Default_TryGetValue_throws_uninitialized()
    {
        var r = default(Result<int, Err>);
        var ex = Assert.Throws<InvalidOperationException>(() => r.TryGetValue(out _, out _));
        Assert.Equal("Result is uninitialized.", ex.Message);
    }

    [Fact]
    public void Default_TryGetError_throws_uninitialized()
    {
        var r = default(Result<int, Err>);
        Assert.Throws<InvalidOperationException>(() => r.TryGetError(out _));
    }

    [Fact]
    public void Default_Match_throws_uninitialized()
    {
        var r = default(Result<int, Err>);
        Assert.Throws<InvalidOperationException>(() => r.Match(_ => { }, _ => { }));
        Assert.Throws<InvalidOperationException>(() => r.Match(_ => 0, _ => 0));
    }

    [Fact]
    public void Default_ValueOrThrow_throws_uninitialized()
    {
        var r = default(Result<int, Err>);
        Assert.Throws<InvalidOperationException>(() => r.ValueOrThrow());
        Assert.Throws<InvalidOperationException>(() => r.ValueOrThrow(_ => new InvalidOperationException()));
    }

    // --- NRT flow-analysis sanity (compiles and runs without warnings) ---

    [Fact]
    public void TryGetValue_NRT_flow_analysis_pattern_compiles_and_runs()
    {
        var r = Result<string, Err>.Success("hello");
        if (r.TryGetValue(out var value, out var error))
        {
            // value is non-null in this branch (analyzer fact).
            Assert.Equal(5, value.Length);
        }
        else
        {
            // error is non-null in this branch.
            Assert.NotNull(error);
        }
    }
}
