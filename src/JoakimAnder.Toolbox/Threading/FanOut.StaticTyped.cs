namespace JoakimAnder.Toolbox.Threading;

public readonly partial struct FanOut
{
    private static readonly Func<CancellationToken, Task>[] NoVoids = [];

    public static async Task<(T1, T2)> WhenAll<T1, T2>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        var r = await FanOutEngine.RunAsync(
            [FanOutEngine.Box(op1), FanOutEngine.Box(op2)], NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!);
    }

    public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        Func<CancellationToken, Task<T3>> op3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        var r = await FanOutEngine.RunAsync(
            [FanOutEngine.Box(op1), FanOutEngine.Box(op2), FanOutEngine.Box(op3)], NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!);
    }

    public static async Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        Func<CancellationToken, Task<T3>> op3,
        Func<CancellationToken, Task<T4>> op4,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        var r = await FanOutEngine.RunAsync(
            [FanOutEngine.Box(op1), FanOutEngine.Box(op2), FanOutEngine.Box(op3), FanOutEngine.Box(op4)],
            NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!);
    }

    public static async Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        Func<CancellationToken, Task<T3>> op3,
        Func<CancellationToken, Task<T4>> op4,
        Func<CancellationToken, Task<T5>> op5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        ArgumentNullException.ThrowIfNull(op5);
        var r = await FanOutEngine.RunAsync(
            [FanOutEngine.Box(op1), FanOutEngine.Box(op2), FanOutEngine.Box(op3), FanOutEngine.Box(op4),
             FanOutEngine.Box(op5)],
            NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!);
    }

    public static async Task<(T1, T2, T3, T4, T5, T6)> WhenAll<T1, T2, T3, T4, T5, T6>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        Func<CancellationToken, Task<T3>> op3,
        Func<CancellationToken, Task<T4>> op4,
        Func<CancellationToken, Task<T5>> op5,
        Func<CancellationToken, Task<T6>> op6,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        ArgumentNullException.ThrowIfNull(op5);
        ArgumentNullException.ThrowIfNull(op6);
        var r = await FanOutEngine.RunAsync(
            [FanOutEngine.Box(op1), FanOutEngine.Box(op2), FanOutEngine.Box(op3), FanOutEngine.Box(op4),
             FanOutEngine.Box(op5), FanOutEngine.Box(op6)],
            NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!, (T6)r[5]!);
    }

    public static async Task<(T1, T2, T3, T4, T5, T6, T7)> WhenAll<T1, T2, T3, T4, T5, T6, T7>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        Func<CancellationToken, Task<T3>> op3,
        Func<CancellationToken, Task<T4>> op4,
        Func<CancellationToken, Task<T5>> op5,
        Func<CancellationToken, Task<T6>> op6,
        Func<CancellationToken, Task<T7>> op7,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        ArgumentNullException.ThrowIfNull(op5);
        ArgumentNullException.ThrowIfNull(op6);
        ArgumentNullException.ThrowIfNull(op7);
        var r = await FanOutEngine.RunAsync(
            [FanOutEngine.Box(op1), FanOutEngine.Box(op2), FanOutEngine.Box(op3), FanOutEngine.Box(op4),
             FanOutEngine.Box(op5), FanOutEngine.Box(op6), FanOutEngine.Box(op7)],
            NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!, (T6)r[5]!, (T7)r[6]!);
    }

    public static async Task<(T1, T2, T3, T4, T5, T6, T7, T8)> WhenAll<T1, T2, T3, T4, T5, T6, T7, T8>(
        Func<CancellationToken, Task<T1>> op1,
        Func<CancellationToken, Task<T2>> op2,
        Func<CancellationToken, Task<T3>> op3,
        Func<CancellationToken, Task<T4>> op4,
        Func<CancellationToken, Task<T5>> op5,
        Func<CancellationToken, Task<T6>> op6,
        Func<CancellationToken, Task<T7>> op7,
        Func<CancellationToken, Task<T8>> op8,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        ArgumentNullException.ThrowIfNull(op5);
        ArgumentNullException.ThrowIfNull(op6);
        ArgumentNullException.ThrowIfNull(op7);
        ArgumentNullException.ThrowIfNull(op8);
        var r = await FanOutEngine.RunAsync(
            [
                FanOutEngine.Box(op1), FanOutEngine.Box(op2), FanOutEngine.Box(op3), FanOutEngine.Box(op4),
                FanOutEngine.Box(op5), FanOutEngine.Box(op6), FanOutEngine.Box(op7), FanOutEngine.Box(op8),
            ], NoVoids, cancellationToken).ConfigureAwait(false);
        return ((T1)r[0]!, (T2)r[1]!, (T3)r[2]!, (T4)r[3]!, (T5)r[4]!, (T6)r[5]!, (T7)r[6]!, (T8)r[7]!);
    }
}
