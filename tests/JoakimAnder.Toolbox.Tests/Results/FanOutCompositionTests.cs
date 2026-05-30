using System.Diagnostics;
using JoakimAnder.Toolbox.Results;
using JoakimAnder.Toolbox.Threading;
using Xunit;

namespace JoakimAnder.Toolbox.Tests.Results;

public class FanOutCompositionTests
{
    private sealed record ApiError(string Code, string Message);

    // --- Pattern 1: wrap FanOut with Result.TryAsync ---

    [Fact]
    public async Task Pattern1_wraps_FanOut_throw_into_typed_failure()
    {
        var result = await Result.TryAsync(
            ct => new FanOut()
                .Add(c => UserAsync(c))
                .Add(c => OrdersAsync(c))
                .WhenAll(ct),
            ex => new ApiError(ex.GetType().Name, ex.Message));

        Assert.True(result.IsFailure);
        Assert.True(result.TryGetError(out var err));
        Assert.Equal(nameof(InvalidOperationException), err.Code);
        Assert.Equal("user-down", err.Message);

        static async Task<string> UserAsync(CancellationToken ct)
        {
            await Task.Delay(10, ct);
            throw new InvalidOperationException("user-down");
        }

        static async Task<int[]> OrdersAsync(CancellationToken ct)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
            catch (OperationCanceledException) { throw; }
            return [1, 2, 3];
        }
    }

    [Fact]
    public async Task Pattern1_FanOut_cancellation_returns_promptly()
    {
        var sw = Stopwatch.StartNew();

        var result = await Result.TryAsync(
            ct => new FanOut()
                .Add<int>(c => Task.FromException<int>(new InvalidOperationException("boom")))
                .Add<int>(async c => { await Task.Delay(TimeSpan.FromSeconds(30), c); return 1; })
                .WhenAll(ct),
            ex => new ApiError("MAPPED", ex.Message));

        sw.Stop();

        Assert.True(result.IsFailure);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"FanOut + TryAsync should return promptly, took {sw.Elapsed}");
    }

    // --- Pattern 2: per-op Results collapsed with Bind ---

    [Fact]
    public async Task Pattern2_collapses_first_failure_with_Bind()
    {
        var (userR, ordersR) = await new FanOut()
            .Add(c => UserAsync(c))
            .Add(c => OrdersAsync(c))
            .WhenAll();

        Result<(string user, int[] orders), ApiError> combined =
            userR.Bind(u => ordersR.Map(o => (u, o)));

        Assert.True(combined.IsFailure);
        Assert.True(combined.TryGetError(out var err));
        Assert.Equal("USER", err.Code);

        static Task<Result<string, ApiError>> UserAsync(CancellationToken ct) =>
            Task.FromResult(Result<string, ApiError>.Failure(new ApiError("USER", "missing")));

        static Task<Result<int[], ApiError>> OrdersAsync(CancellationToken ct) =>
            Task.FromResult(Result<int[], ApiError>.Success([1, 2]));
    }

    [Fact]
    public async Task Pattern2_does_not_cancel_slow_sibling_on_Result_failure()
    {
        // This codifies the documented limitation: FanOut sees a Result.Failure
        // as a successfully-completed Task. The slow sibling runs to completion
        // because there is no fault to cancel it. Use Pattern 1 if you want
        // fail-fast cancellation on typed errors too.

        var siblingRan = false;
        var sw = Stopwatch.StartNew();

        var (failingR, slowR) = await new FanOut()
            .Add(c => FailingAsync(c))
            .Add(c => SlowAsync(c))
            .WhenAll();

        sw.Stop();

        Assert.True(failingR.IsFailure);
        Assert.True(slowR.IsSuccess);
        Assert.True(siblingRan, "sibling should have completed, not been cancelled");
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(200),
            $"slow sibling should have run to completion (~250ms), took {sw.Elapsed}");

        static Task<Result<string, ApiError>> FailingAsync(CancellationToken ct) =>
            Task.FromResult(Result<string, ApiError>.Failure(new ApiError("X", "x")));

        async Task<Result<int, ApiError>> SlowAsync(CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
            siblingRan = true;
            return Result<int, ApiError>.Success(1);
        }
    }
}
