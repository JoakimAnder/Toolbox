using System.Diagnostics;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Results;
using JoakimAnder.Toolbox.Threading;
using Microsoft.Extensions.DependencyInjection;

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
// Only GetUserAsync faults here (InvalidOperationException); real code would catch Exception.
catch (InvalidOperationException ex)
{
    Console.WriteLine($"\nFan-out failed after {sw.ElapsedMilliseconds} ms: {ex.Message}");
    Console.WriteLine("(Audit already completed; the 30s order fetch was cancelled before it could finish.)");
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

// --- DI attributes + source generator demo ---
Console.WriteLine("\nDI attributes + source generator demo\n");

var services = new ServiceCollection()
    .AddAttributedServices()
    .AddAttributedWebServices()
    .BuildServiceProvider();

Console.WriteLine($"IClock -> {services.GetRequiredService<IClock>().GetType().Name}");
Console.WriteLine($"keyed 'redis' ICache -> {services.GetRequiredKeyedService<ICache>("redis").GetType().Name}");
Console.WriteLine($"web IGreeter -> {services.GetRequiredService<IGreeter>().GetType().Name}");

// --- Result + FanOut composition demo ---
Console.WriteLine("\nResult example: explicit failure flow at boundaries\n");

var resultOutcome = await GetUserSummaryAsync(userId: 42, cancellationToken: default);

var label = resultOutcome.Match(
    onSuccess: summary => $"OK: user '{summary.Name}' with {summary.OrderCount} orders",
    onFailure: err => err switch
    {
        ApiError.NotFound nf  => $"404 Not Found: {nf.Message}",
        ApiError.Upstream up  => $"502 Upstream ({up.ExceptionType}): {up.Message}",
        ApiError.Unexpected u => $"500 Unexpected: {u.Message}",
        _                     => $"unmapped error: {err}"
    });

Console.WriteLine(label);

static async Task<Result<UserSummary, ApiError>> GetUserSummaryAsync(
    int userId, CancellationToken cancellationToken)
{
    return await Result.TryAsync(
        async ct =>
        {
            var (user, orders) = await new FanOut()
                .Add(c => FetchUserAsync(userId, c))
                .Add(c => FetchOrdersAsync(userId, c))
                .WhenAll(ct);

            return new UserSummary(user, orders.Length);
        },
        ex => ex switch
        {
            KeyNotFoundException knf => (ApiError)new ApiError.NotFound(knf.Message),
            HttpRequestException hre => new ApiError.Upstream(nameof(HttpRequestException), hre.Message),
            _                         => new ApiError.Unexpected(ex.Message)
        },
        cancellationToken);

    // Deliberately fails: throws HttpRequestException so the boundary maps it to ApiError.Upstream.
    static async Task<string> FetchUserAsync(int id, CancellationToken ct)
    {
        await Task.Delay(20, ct);
        throw new HttpRequestException("upstream user service refused connection");
    }

    static async Task<int[]> FetchOrdersAsync(int id, CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
        catch (OperationCanceledException) { throw; }
        return [];
    }
}

// --- Type declarations ---

interface IClock { }
[Singleton(typeof(IClock))] sealed class SystemClock : IClock { }

interface ICache { }
[Singleton(typeof(ICache), Key = "redis")] sealed class RedisCache : ICache { }

interface IGreeter { }
[Scoped(typeof(IGreeter), Group = "Web")] sealed class Greeter : IGreeter { }

internal sealed record UserSummary(string Name, int OrderCount);

internal abstract record ApiError
{
    public sealed record NotFound(string Message) : ApiError;
    public sealed record Upstream(string ExceptionType, string Message) : ApiError;
    public sealed record Unexpected(string Message) : ApiError;
}
