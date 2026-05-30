namespace JoakimAnder.Toolbox.Results;

/// <summary>
/// Extension methods that lift the sync Result operators to <see cref="Task{TResult}"/>
/// of Result, so chains compose naturally across <c>await</c> boundaries.
/// Each public method validates arguments synchronously (so null-task errors surface at
/// the call site) and then delegates to a private <c>async</c> core method.
/// </summary>
public static class TaskResultExtensions
{
    // -------------------------------------------------------------------------
    // Result<T, TError> extensions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Awaits <paramref name="task"/> and transforms the success value with
    /// <paramref name="map"/>; passes the failure through unchanged.
    /// </summary>
    public static Task<Result<TResult, TError>> Map<T, TResult, TError>(
        this Task<Result<T, TError>> task,
        Func<T, TResult> map) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(map);
        return MapCore(task, map);
    }

    private static async Task<Result<TResult, TError>> MapCore<T, TResult, TError>(
        Task<Result<T, TError>> task,
        Func<T, TResult> map) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.Map(map);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and transforms the failure value with
    /// <paramref name="map"/>; passes the success through unchanged.
    /// </summary>
    public static Task<Result<T, TNewError>> MapError<T, TError, TNewError>(
        this Task<Result<T, TError>> task,
        Func<TError, TNewError> map)
        where TError : notnull
        where TNewError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(map);
        return MapErrorCore(task, map);
    }

    private static async Task<Result<T, TNewError>> MapErrorCore<T, TError, TNewError>(
        Task<Result<T, TError>> task,
        Func<TError, TNewError> map)
        where TError : notnull
        where TNewError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.MapError(map);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and chains a sync Result-returning step;
    /// failure short-circuits.
    /// </summary>
    public static Task<Result<TResult, TError>> Bind<T, TResult, TError>(
        this Task<Result<T, TError>> task,
        Func<T, Result<TResult, TError>> next) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);
        return BindCore(task, next);
    }

    private static async Task<Result<TResult, TError>> BindCore<T, TResult, TError>(
        Task<Result<T, TError>> task,
        Func<T, Result<TResult, TError>> next) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.Bind(next);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and chains an async Result-returning step;
    /// failure short-circuits without invoking <paramref name="next"/>.
    /// </summary>
    public static Task<Result<TResult, TError>> BindAsync<T, TResult, TError>(
        this Task<Result<T, TError>> task,
        Func<T, Task<Result<TResult, TError>>> next) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);
        return BindAsyncCore(task, next);
    }

    private static async Task<Result<TResult, TError>> BindAsyncCore<T, TResult, TError>(
        Task<Result<T, TError>> task,
        Func<T, Task<Result<TResult, TError>>> next) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        if (r.TryGetValue(out var value, out var error))
        {
            return await next(value).ConfigureAwait(false);
        }

        return Result<TResult, TError>.Failure(error);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and returns the result of the matching delegate.
    /// </summary>
    public static Task<TOut> Match<T, TError, TOut>(
        this Task<Result<T, TError>> task,
        Func<T, TOut> onSuccess,
        Func<TError, TOut> onFailure) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return MatchCore(task, onSuccess, onFailure);
    }

    private static async Task<TOut> MatchCore<T, TError, TOut>(
        Task<Result<T, TError>> task,
        Func<T, TOut> onSuccess,
        Func<TError, TOut> onFailure) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.Match(onSuccess, onFailure);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and asynchronously invokes the matching branch.
    /// </summary>
    public static Task<TOut> MatchAsync<T, TError, TOut>(
        this Task<Result<T, TError>> task,
        Func<T, Task<TOut>> onSuccess,
        Func<TError, Task<TOut>> onFailure) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return MatchAsyncCore(task, onSuccess, onFailure);
    }

    private static async Task<TOut> MatchAsyncCore<T, TError, TOut>(
        Task<Result<T, TError>> task,
        Func<T, Task<TOut>> onSuccess,
        Func<TError, Task<TOut>> onFailure) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.TryGetValue(out var value, out var error)
            ? await onSuccess(value).ConfigureAwait(false)
            : await onFailure(error).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and returns the success value, or throws
    /// <see cref="InvalidOperationException"/> on failure.
    /// </summary>
    public static Task<T> ValueOrThrowAsync<T, TError>(
        this Task<Result<T, TError>> task) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        return ValueOrThrowAsyncCore(task);
    }

    private static async Task<T> ValueOrThrowAsyncCore<T, TError>(
        Task<Result<T, TError>> task) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.ValueOrThrow();
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and returns the success value, or throws the
    /// exception produced by <paramref name="exceptionMapper"/> on failure.
    /// </summary>
    public static Task<T> ValueOrThrowAsync<T, TError>(
        this Task<Result<T, TError>> task,
        Func<TError, Exception> exceptionMapper) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(exceptionMapper);
        return ValueOrThrowAsyncCore(task, exceptionMapper);
    }

    private static async Task<T> ValueOrThrowAsyncCore<T, TError>(
        Task<Result<T, TError>> task,
        Func<TError, Exception> exceptionMapper) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.ValueOrThrow(exceptionMapper);
    }

    // -------------------------------------------------------------------------
    // Result<TError> extensions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Awaits <paramref name="task"/> and transforms the failure value with
    /// <paramref name="map"/>; passes success through unchanged.
    /// </summary>
    public static Task<Result<TNewError>> MapError<TError, TNewError>(
        this Task<Result<TError>> task,
        Func<TError, TNewError> map)
        where TError : notnull
        where TNewError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(map);
        return MapErrorVoidCore(task, map);
    }

    private static async Task<Result<TNewError>> MapErrorVoidCore<TError, TNewError>(
        Task<Result<TError>> task,
        Func<TError, TNewError> map)
        where TError : notnull
        where TNewError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.MapError(map);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and chains a sync void-Result-returning step;
    /// failure short-circuits.
    /// </summary>
    public static Task<Result<TError>> Bind<TError>(
        this Task<Result<TError>> task,
        Func<Result<TError>> next) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);
        return BindVoidCore(task, next);
    }

    private static async Task<Result<TError>> BindVoidCore<TError>(
        Task<Result<TError>> task,
        Func<Result<TError>> next) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.Bind(next);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and chains a sync typed-Result-returning step;
    /// failure short-circuits.
    /// </summary>
    public static Task<Result<TResult, TError>> Bind<TResult, TError>(
        this Task<Result<TError>> task,
        Func<Result<TResult, TError>> next) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);
        return BindVoidToTypedCore(task, next);
    }

    private static async Task<Result<TResult, TError>> BindVoidToTypedCore<TResult, TError>(
        Task<Result<TError>> task,
        Func<Result<TResult, TError>> next) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.Bind(next);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and chains an async void-Result-returning step;
    /// failure short-circuits without invoking <paramref name="next"/>.
    /// </summary>
    public static Task<Result<TError>> BindAsync<TError>(
        this Task<Result<TError>> task,
        Func<Task<Result<TError>>> next) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);
        return BindAsyncVoidCore(task, next);
    }

    private static async Task<Result<TError>> BindAsyncVoidCore<TError>(
        Task<Result<TError>> task,
        Func<Task<Result<TError>>> next) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        if (r.TryGetError(out var error))
        {
            return Result<TError>.Failure(error);
        }

        return await next().ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and chains an async typed-Result-returning step;
    /// failure short-circuits without invoking <paramref name="next"/>.
    /// </summary>
    public static Task<Result<TResult, TError>> BindAsync<TResult, TError>(
        this Task<Result<TError>> task,
        Func<Task<Result<TResult, TError>>> next) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);
        return BindAsyncVoidToTypedCore(task, next);
    }

    private static async Task<Result<TResult, TError>> BindAsyncVoidToTypedCore<TResult, TError>(
        Task<Result<TError>> task,
        Func<Task<Result<TResult, TError>>> next) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        if (r.TryGetError(out var error))
        {
            return Result<TResult, TError>.Failure(error);
        }

        return await next().ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and returns the result of the matching delegate.
    /// </summary>
    public static Task<TOut> Match<TError, TOut>(
        this Task<Result<TError>> task,
        Func<TOut> onSuccess,
        Func<TError, TOut> onFailure) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return MatchVoidCore(task, onSuccess, onFailure);
    }

    private static async Task<TOut> MatchVoidCore<TError, TOut>(
        Task<Result<TError>> task,
        Func<TOut> onSuccess,
        Func<TError, TOut> onFailure) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.Match(onSuccess, onFailure);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and asynchronously invokes the matching branch.
    /// </summary>
    public static Task<TOut> MatchAsync<TError, TOut>(
        this Task<Result<TError>> task,
        Func<Task<TOut>> onSuccess,
        Func<TError, Task<TOut>> onFailure) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return MatchAsyncVoidCore(task, onSuccess, onFailure);
    }

    private static async Task<TOut> MatchAsyncVoidCore<TError, TOut>(
        Task<Result<TError>> task,
        Func<Task<TOut>> onSuccess,
        Func<TError, Task<TOut>> onFailure) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        return r.TryGetError(out var error)
            ? await onFailure(error).ConfigureAwait(false)
            : await onSuccess().ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and returns silently on success, or throws
    /// <see cref="InvalidOperationException"/> on failure.
    /// </summary>
    public static Task ThrowIfFailureAsync<TError>(
        this Task<Result<TError>> task) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        return ThrowIfFailureAsyncCore(task);
    }

    private static async Task ThrowIfFailureAsyncCore<TError>(
        Task<Result<TError>> task) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        r.ThrowIfFailure();
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and returns silently on success. On failure, throws
    /// the exception produced by <paramref name="exceptionMapper"/>.
    /// </summary>
    public static Task ThrowIfFailureAsync<TError>(
        this Task<Result<TError>> task,
        Func<TError, Exception> exceptionMapper) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(exceptionMapper);
        return ThrowIfFailureAsyncCore(task, exceptionMapper);
    }

    private static async Task ThrowIfFailureAsyncCore<TError>(
        Task<Result<TError>> task,
        Func<TError, Exception> exceptionMapper) where TError : notnull
    {
        var r = await task.ConfigureAwait(false);
        r.ThrowIfFailure(exceptionMapper);
    }
}
