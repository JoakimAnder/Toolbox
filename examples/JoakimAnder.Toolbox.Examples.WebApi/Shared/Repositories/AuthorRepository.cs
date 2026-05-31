using System.Collections.Concurrent;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;

/// <summary>
/// In-memory author repository. Read-only (no write surface in this example).
/// </summary>
[Singleton]
public sealed class AuthorRepository
{
    private readonly ConcurrentDictionary<int, Author> _authors = new();
    private readonly Random _latencyRandom = new(7);

    public AuthorRepository()
    {
        _authors[1] = new Author(1, "Andy Hunt");
        _authors[2] = new Author(2, "Eric Evans");
        _authors[3] = new Author(3, "Michael Feathers");
    }

    public async Task<Author?> GetAsync(int id, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);
        return _authors.TryGetValue(id, out var author) ? author : null;
    }

    private Task SimulateLatencyAsync(CancellationToken ct)
    {
        int delay;
        lock (_latencyRandom) { delay = _latencyRandom.Next(15, 30); }
        return Task.Delay(delay, ct);
    }
}
