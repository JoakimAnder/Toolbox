using System.Collections.Concurrent;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;

/// <summary>
/// In-memory book repository. Singleton — seed data persists across requests
/// for the lifetime of the host process. Each method simulates ~15–30 ms of
/// I/O latency so FanOut composition is observable.
/// </summary>
[Singleton]
public sealed class BookRepository
{
    private readonly ConcurrentDictionary<int, Book> _books = new();
    // Seeded for reproducible latency in manual smoke tests. The lock in SimulateLatencyAsync
    // is required because System.Random instance methods are not thread-safe; the seed prevents
    // using Random.Shared, which is thread-safe but not seedable.
    private readonly Random _latencyRandom = new(42);
    private int _nextId;

    public BookRepository()
    {
        Seed(new Book(1, "978-0-13-468599-1", "The Pragmatic Programmer", AuthorId: 1));
        Seed(new Book(2, "978-0-13-475759-9", "Refactoring",              AuthorId: 1));
        Seed(new Book(3, "978-0-321-12521-7", "Domain-Driven Design",     AuthorId: 2));
        Seed(new Book(4, "978-0-321-20068-6", "Working Effectively with Legacy Code", AuthorId: 3));
        Seed(new Book(5, "978-0-13-235088-4", "Clean Code",               AuthorId: 1));
    }

    public async Task<Book?> GetAsync(int id, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);
        return _books.TryGetValue(id, out var book) ? book : null;
    }

    public async Task<IReadOnlyList<Book>> ListAsync(CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);
        return _books.Values.OrderBy(b => b.Id).ToList();
    }

    /// <summary>
    /// Persists <paramref name="book"/> with a newly assigned Id. Returns
    /// <c>null</c> if the ISBN already exists.
    /// </summary>
    public async Task<Book?> AddAsync(Book book, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);

        lock (_books)
        {
            // ISBN uniqueness constraint (case-insensitive).
            if (_books.Values.Any(b => string.Equals(b.Isbn, book.Isbn, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var id = Interlocked.Increment(ref _nextId);
            var stored = book with { Id = id };
            // TryAdd cannot fail here: id is globally unique via Interlocked.Increment.
            return _books.TryAdd(id, stored) ? stored : null;
        }
    }

    private void Seed(Book book)
    {
        _books[book.Id] = book;
        if (book.Id > _nextId)
        {
            _nextId = book.Id;
        }
    }

    private Task SimulateLatencyAsync(CancellationToken ct)
    {
        int delay;
        lock (_latencyRandom) { delay = _latencyRandom.Next(15, 30); }
        return Task.Delay(delay, ct);
    }
}
