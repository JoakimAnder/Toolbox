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
