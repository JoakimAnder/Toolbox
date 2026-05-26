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
    public void Enumerable_overload_null_collection_throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => { _ = FanOut.WhenAll((IEnumerable<Func<CancellationToken, Task>>)null!); });
    }

    [Fact]
    public void Enumerable_overload_null_element_throws()
    {
        var ops = new Func<CancellationToken, Task>[] { _ => Task.CompletedTask, null! };
        Assert.Throws<ArgumentException>(
            () => { _ = FanOut.WhenAll(ops); });
    }
}
