namespace JoakimAnder.Toolbox.Results;

public static partial class Result
{
    /// <summary>
    /// Invokes <paramref name="action"/>. If it returns a value, wraps it in a success Result.
    /// If it throws any exception other than <see cref="OperationCanceledException"/> (including
    /// <see cref="TaskCanceledException"/>), invokes <paramref name="onException"/> to map the
    /// exception to a typed error and returns a failure Result. <c>OperationCanceledException</c>
    /// is rethrown unchanged so cancellation contracts stay intact.
    /// </summary>
    public static Result<T, TError> Try<T, TError>(
        Func<T> action,
        Func<Exception, TError> onException) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onException);

        try
        {
            return Result<T, TError>.Success(action());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(onException(ex));
        }
    }

    /// <summary>Void variant of <see cref="Try{T, TError}(Func{T}, Func{Exception, TError})"/>.</summary>
    public static Result<TError> Try<TError>(
        Action action,
        Func<Exception, TError> onException) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onException);

        try
        {
            action();
            return Result<TError>.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<TError>.Failure(onException(ex));
        }
    }

    /// <summary>
    /// Awaits <paramref name="action"/> with a linked cancellation token. Maps any non-OCE
    /// exception to a typed error; rethrows <see cref="OperationCanceledException"/> unchanged.
    /// </summary>
    public static async Task<Result<T, TError>> TryAsync<T, TError>(
        Func<CancellationToken, Task<T>> action,
        Func<Exception, TError> onException,
        CancellationToken cancellationToken = default) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onException);

        try
        {
            var value = await action(cancellationToken).ConfigureAwait(false);
            return Result<T, TError>.Success(value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(onException(ex));
        }
    }

    /// <summary>Void variant of the async <c>TryAsync</c>.</summary>
    public static async Task<Result<TError>> TryAsync<TError>(
        Func<CancellationToken, Task> action,
        Func<Exception, TError> onException,
        CancellationToken cancellationToken = default) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onException);

        try
        {
            await action(cancellationToken).ConfigureAwait(false);
            return Result<TError>.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<TError>.Failure(onException(ex));
        }
    }
}
