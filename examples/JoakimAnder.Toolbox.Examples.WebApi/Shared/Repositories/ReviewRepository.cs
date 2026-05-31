using System.Collections.Concurrent;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;

/// <summary>
/// In-memory review repository. Reviews are unique per (BookId, Reviewer).
/// </summary>
[Singleton]
public sealed class ReviewRepository
{
    private readonly ConcurrentDictionary<int, Review> _reviews = new();
    private readonly Random _latencyRandom = new(13);
    private int _nextId;

    public ReviewRepository()
    {
        Seed(new Review(1, BookId: 1, "alice",  Rating: 5, "Foundational; reread every other year."));
        Seed(new Review(2, BookId: 1, "bob",    Rating: 4, "Solid. The chapter on Coupling is gold."));
        Seed(new Review(3, BookId: 2, "alice",  Rating: 5, "The catalogue alone is worth it."));
        Seed(new Review(4, BookId: 3, "carol",  Rating: 5, "DDD finally clicked after this."));
        Seed(new Review(5, BookId: 3, "dave",   Rating: 4, "Dense in places; pace yourself."));
        Seed(new Review(6, BookId: 4, "carol",  Rating: 5, "Survival manual."));
        Seed(new Review(7, BookId: 5, "alice",  Rating: 4, "Some advice has aged better than others."));
        Seed(new Review(8, BookId: 5, "eve",    Rating: 3, "Good ideas, repetitive."));
    }

    public async Task<IReadOnlyList<Review>> GetByBookAsync(int bookId, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);
        return _reviews.Values
            .Where(r => r.BookId == bookId)
            .OrderBy(r => r.Id)
            .ToList();
    }

    /// <summary>
    /// Persists <paramref name="review"/> with a newly assigned Id. Returns
    /// <c>null</c> if (BookId, Reviewer) already has a review.
    /// </summary>
    public async Task<Review?> AddAsync(Review review, CancellationToken ct = default)
    {
        await SimulateLatencyAsync(ct).ConfigureAwait(false);

        if (_reviews.Values.Any(r =>
                r.BookId == review.BookId &&
                string.Equals(r.Reviewer, review.Reviewer, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var id = Interlocked.Increment(ref _nextId);
        var stored = review with { Id = id };
        return _reviews.TryAdd(id, stored) ? stored : null;
    }

    private void Seed(Review review)
    {
        _reviews[review.Id] = review;
        if (review.Id > _nextId)
        {
            _nextId = review.Id;
        }
    }

    private Task SimulateLatencyAsync(CancellationToken ct)
    {
        int delay;
        lock (_latencyRandom) { delay = _latencyRandom.Next(15, 30); }
        return Task.Delay(delay, ct);
    }
}
