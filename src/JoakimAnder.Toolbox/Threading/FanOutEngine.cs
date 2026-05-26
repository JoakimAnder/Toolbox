using System.Runtime.ExceptionServices;

namespace JoakimAnder.Toolbox.Threading;

internal static class FanOutEngine
{
    public static async Task<object?[]> RunAsync(
        Func<CancellationToken, Task<object?>>[] resultOps,
        Func<CancellationToken, Task>[] voidOps,
        CancellationToken cancellationToken)
    {
        var total = resultOps.Length + voidOps.Length;
        if (total == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;

        var resultTasks = new Task<object?>[resultOps.Length];
        for (var i = 0; i < resultOps.Length; i++)
        {
            resultTasks[i] = InvokeResult(resultOps[i], token);
        }

        var remaining = new List<Task>(total);
        remaining.AddRange(resultTasks);
        for (var i = 0; i < voidOps.Length; i++)
        {
            remaining.Add(Invoke(voidOps[i], token));
        }

        Exception? trigger = null;
        while (remaining.Count > 0)
        {
            var completed = await Task.WhenAny(remaining).ConfigureAwait(false);
            remaining.Remove(completed);

            if (trigger is null && completed.IsFaulted)
            {
                var aggregate = completed.Exception!;
                trigger = aggregate.InnerExceptions.Count == 1 ? aggregate.InnerExceptions[0] : aggregate;
                if (!linked.IsCancellationRequested)
                {
                    linked.Cancel();
                }
            }
        }

        if (trigger is not null)
        {
            ExceptionDispatchInfo.Throw(trigger);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var results = new object?[resultTasks.Length];
        for (var i = 0; i < resultTasks.Length; i++)
        {
            results[i] = resultTasks[i].Result;
        }

        return results;
    }

    public static Func<CancellationToken, Task<object?>> Box<T>(Func<CancellationToken, Task<T>> operation)
        => async ct =>
        {
            var task = operation(ct)
                ?? throw new InvalidOperationException("Operation factory returned a null Task.");
            return await task.ConfigureAwait(false);
        };

    private static Task<object?> InvokeResult(Func<CancellationToken, Task<object?>> op, CancellationToken token)
    {
        try
        {
            return op(token)
                ?? Task.FromException<object?>(new InvalidOperationException("Operation factory returned a null Task."));
        }
        catch (Exception ex)
        {
            return Task.FromException<object?>(ex);
        }
    }

    private static Task Invoke(Func<CancellationToken, Task> op, CancellationToken token)
    {
        try
        {
            return op(token)
                ?? Task.FromException(new InvalidOperationException("Operation factory returned a null Task."));
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }
}
