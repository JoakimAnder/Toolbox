namespace JoakimAnder.Toolbox.Threading;

/// <summary>Fan-out builder carrying one result type. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2> Add<T2>(Func<CancellationToken, Task<T2>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    public FanOut<T1> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<T1> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return (T1)r[0]!;
    }
}

/// <summary>Fan-out builder carrying two result types. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    // NOTE (Task 1 interim): arity 2 is the temporary ceiling — no Add<T3> yet.
    // Task 2 adds the Add<T3> method here once FanOut<T1, T2, T3> exists.

    public FanOut<T1, T2> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!);
    }
}
