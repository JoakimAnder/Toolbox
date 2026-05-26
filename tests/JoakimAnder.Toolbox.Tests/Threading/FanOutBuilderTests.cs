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
    public async Task Faulting_void_op_propagates_unwrapped_and_cancels_siblings()
    {
        var observed = false;
        var builder = new FanOut()
            .Add<int>(async ct =>
            {
                try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
                catch (OperationCanceledException) { observed = true; throw; }
                return 1;
            })
            .Add(_ => Task.FromException(new InvalidOperationException("void boom")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.WhenAll());

        Assert.Equal("void boom", ex.Message);
        Assert.True(observed, "slow sibling should have observed cancellation");
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
}
