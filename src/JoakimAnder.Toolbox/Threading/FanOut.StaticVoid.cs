namespace JoakimAnder.Toolbox.Threading;

public readonly partial struct FanOut
{
    private static readonly Func<CancellationToken, Task<object?>>[] NoResults = [];

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        return FanOutEngine.RunAsync(NoResults, [op1, op2], cancellationToken);
    }

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        Func<CancellationToken, Task> op3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        return FanOutEngine.RunAsync(NoResults, [op1, op2, op3], cancellationToken);
    }

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        Func<CancellationToken, Task> op3,
        Func<CancellationToken, Task> op4,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        return FanOutEngine.RunAsync(NoResults, [op1, op2, op3, op4], cancellationToken);
    }

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        Func<CancellationToken, Task> op3,
        Func<CancellationToken, Task> op4,
        Func<CancellationToken, Task> op5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        ArgumentNullException.ThrowIfNull(op5);
        return FanOutEngine.RunAsync(NoResults, [op1, op2, op3, op4, op5], cancellationToken);
    }

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        Func<CancellationToken, Task> op3,
        Func<CancellationToken, Task> op4,
        Func<CancellationToken, Task> op5,
        Func<CancellationToken, Task> op6,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        ArgumentNullException.ThrowIfNull(op5);
        ArgumentNullException.ThrowIfNull(op6);
        return FanOutEngine.RunAsync(NoResults, [op1, op2, op3, op4, op5, op6], cancellationToken);
    }

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        Func<CancellationToken, Task> op3,
        Func<CancellationToken, Task> op4,
        Func<CancellationToken, Task> op5,
        Func<CancellationToken, Task> op6,
        Func<CancellationToken, Task> op7,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op1);
        ArgumentNullException.ThrowIfNull(op2);
        ArgumentNullException.ThrowIfNull(op3);
        ArgumentNullException.ThrowIfNull(op4);
        ArgumentNullException.ThrowIfNull(op5);
        ArgumentNullException.ThrowIfNull(op6);
        ArgumentNullException.ThrowIfNull(op7);
        return FanOutEngine.RunAsync(NoResults, [op1, op2, op3, op4, op5, op6, op7], cancellationToken);
    }

    public static Task WhenAll(
        Func<CancellationToken, Task> op1,
        Func<CancellationToken, Task> op2,
        Func<CancellationToken, Task> op3,
        Func<CancellationToken, Task> op4,
        Func<CancellationToken, Task> op5,
        Func<CancellationToken, Task> op6,
        Func<CancellationToken, Task> op7,
        Func<CancellationToken, Task> op8,
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
        return FanOutEngine.RunAsync(NoResults, [op1, op2, op3, op4, op5, op6, op7, op8], cancellationToken);
    }

    public static Task WhenAll(
        IEnumerable<Func<CancellationToken, Task>> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var array = operations.ToArray();
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i] is null)
            {
                throw new ArgumentException("Operations contains a null element.", nameof(operations));
            }
        }

        return FanOutEngine.RunAsync(NoResults, array, cancellationToken);
    }
}
