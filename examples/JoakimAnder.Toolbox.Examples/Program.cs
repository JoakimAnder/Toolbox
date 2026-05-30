using System.Diagnostics;
using JoakimAnder.Toolbox.DependencyInjection;
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

interface IClock { }
[Singleton(typeof(IClock))] sealed class SystemClock : IClock { }

interface ICache { }
[Singleton(typeof(ICache), Key = "redis")] sealed class RedisCache : ICache { }

interface IGreeter { }
[Scoped(typeof(IGreeter), Group = "Web")] sealed class Greeter : IGreeter { }
