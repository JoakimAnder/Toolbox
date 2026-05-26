namespace JoakimAnder.Toolbox.Threading;

/// <summary>
/// Fail-fast fan-out of asynchronous operations that must all succeed. The first
/// operation to fault cancels the linked token handed to the rest, and its
/// exception is rethrown unwrapped. Immutable: each <c>Add</c> returns a new builder.
/// </summary>
// The static WhenAll convenience overloads (accepting Task/Task<T> delegates directly) land in later partial declarations (Tasks 3 and 4).
public readonly partial struct FanOut
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    /// <summary>Creates an empty builder. Equivalent to <c>new FanOut()</c>.</summary>
    public static FanOut Create() => default;

    /// <summary>Adds a result-producing operation; returns the next-arity builder.</summary>
    public FanOut<T1> Add<T1>(Func<CancellationToken, Task<T1>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    /// <summary>Adds a void "must also succeed" operation; returns the same-arity builder.</summary>
    public FanOut Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut(_results, FanOutArray.Append(_voids, operation));
    }

    /// <summary>Runs all added operations fail-fast; completes when all succeed.</summary>
    public Task WhenAll(CancellationToken cancellationToken = default)
        => FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken);
}
