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

    public FanOut<T1, T2, T3> Add<T3>(Func<CancellationToken, Task<T3>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

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

/// <summary>Fan-out builder carrying three result types. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2, T3>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2, T3, T4> Add<T4>(Func<CancellationToken, Task<T4>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    public FanOut<T1, T2, T3> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2, T3)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!);
    }
}

/// <summary>Fan-out builder carrying four result types. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2, T3, T4>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2, T3, T4, T5> Add<T5>(Func<CancellationToken, Task<T5>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4, T5>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    public FanOut<T1, T2, T3, T4> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2, T3, T4)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!);
    }
}

/// <summary>Fan-out builder carrying five result types. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2, T3, T4, T5>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2, T3, T4, T5, T6> Add<T6>(Func<CancellationToken, Task<T6>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4, T5, T6>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    public FanOut<T1, T2, T3, T4, T5> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4, T5>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2, T3, T4, T5)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!);
    }
}

/// <summary>Fan-out builder carrying six result types. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2, T3, T4, T5, T6>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2, T3, T4, T5, T6, T7> Add<T7>(Func<CancellationToken, Task<T7>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4, T5, T6, T7>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    public FanOut<T1, T2, T3, T4, T5, T6> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4, T5, T6>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2, T3, T4, T5, T6)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!, (T6)r[5]!);
    }
}

/// <summary>Fan-out builder carrying seven result types. See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2, T3, T4, T5, T6, T7>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2, T3, T4, T5, T6, T7, T8> Add<T8>(Func<CancellationToken, Task<T8>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4, T5, T6, T7, T8>(FanOutArray.Append(_results, FanOutEngine.Box(operation)), _voids);
    }

    public FanOut<T1, T2, T3, T4, T5, T6, T7> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4, T5, T6, T7>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2, T3, T4, T5, T6, T7)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!, (T6)r[5]!, (T7)r[6]!);
    }
}

/// <summary>Fan-out builder carrying eight result types (the arity ceiling). See <see cref="FanOut"/>.</summary>
public readonly struct FanOut<T1, T2, T3, T4, T5, T6, T7, T8>
{
    private readonly Func<CancellationToken, Task<object?>>[]? _results;
    private readonly Func<CancellationToken, Task>[]? _voids;

    internal FanOut(Func<CancellationToken, Task<object?>>[]? results, Func<CancellationToken, Task>[]? voids)
    {
        _results = results;
        _voids = voids;
    }

    public FanOut<T1, T2, T3, T4, T5, T6, T7, T8> Add(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new FanOut<T1, T2, T3, T4, T5, T6, T7, T8>(_results, FanOutArray.Append(_voids, operation));
    }

    public async Task<(T1, T2, T3, T4, T5, T6, T7, T8)> WhenAll(CancellationToken cancellationToken = default)
    {
        var r = await FanOutEngine.RunAsync(FanOutArray.OrEmpty(_results), FanOutArray.OrEmpty(_voids), cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!, (T6)r[5]!, (T7)r[6]!, (T8)r[7]!);
    }
}
