using System.Diagnostics.CodeAnalysis;

namespace JoakimAnder.Toolbox.Results;

/// <summary>
/// A typed success-or-failure container. Either holds a <typeparamref name="T"/> (success)
/// or a <typeparamref name="TError"/> (failure). The default value is "uninitialized" and
/// throws on every operation except <see cref="IsSuccess"/>, <see cref="IsFailure"/>, and
/// <see cref="IsDefault"/>.
/// </summary>
public readonly struct Result<T, TError> where TError : notnull
{
    private const byte StateUninitialized = 0;
    private const byte StateSuccess = 1;
    private const byte StateFailure = 2;

    private readonly byte _state;
    private readonly T? _value;
    private readonly TError? _error;

    private Result(T value)
    {
        _state = StateSuccess;
        _value = value;
        _error = default;
    }

    private Result(TError error)
    {
        _state = StateFailure;
        _value = default;
        _error = error;
    }

#pragma warning disable CA1000 // Do not declare static members on generic types — intentional factory API
    /// <summary>Creates a success result holding <paramref name="value"/>.</summary>
    public static Result<T, TError> Success(T value) => new(value);

    /// <summary>Creates a failure result holding <paramref name="error"/>.</summary>
    public static Result<T, TError> Failure(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(error);
    }
#pragma warning restore CA1000

    public static implicit operator Result<T, TError>(T value) => new(value);
    public static implicit operator Result<T, TError>(TError error) => Failure(error);
    public static implicit operator Result<T, TError>(Success<T> success) => new(success.Value);
    public static implicit operator Result<T, TError>(Failure<TError> failure) => new(failure.Error);

    /// <summary>True iff the result holds a success value.</summary>
    public bool IsSuccess => _state == StateSuccess;

    /// <summary>True iff the result holds a failure value.</summary>
    public bool IsFailure => _state == StateFailure;

    /// <summary>True iff the result is the default value (never constructed).</summary>
    public bool IsDefault => _state == StateUninitialized;

    /// <summary>
    /// On success, sets <paramref name="value"/> and returns true; on failure, sets
    /// <paramref name="error"/> and returns false. Throws on a default-state Result.
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value, [NotNullWhen(false)] out TError error)
    {
        ThrowIfUninitialized();
        if (_state == StateSuccess)
        {
            value = _value!;
            error = default!;
            return true;
        }

        value = default;
        error = _error!;
        return false;
    }

    /// <summary>
    /// On failure, sets <paramref name="error"/> and returns true; on success, returns false
    /// with <paramref name="error"/> set to the default. Throws on a default-state Result.
    /// </summary>
    public bool TryGetError([NotNullWhen(true)] out TError error)
    {
        ThrowIfUninitialized();
        if (_state == StateFailure)
        {
            error = _error!;
            return true;
        }

        error = default!;
        return false;
    }

    /// <summary>Invokes the matching delegate for the current state. Throws on default.</summary>
    public void Match(Action<T> onSuccess, Action<TError> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ThrowIfUninitialized();

        if (_state == StateSuccess)
        {
            onSuccess(_value!);
        }
        else
        {
            onFailure(_error!);
        }
    }

    /// <summary>Returns the result of the matching delegate. Throws on default.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<TError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ThrowIfUninitialized();

        return _state == StateSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>
    /// Returns the success value. Throws <see cref="InvalidOperationException"/> with the
    /// error's <c>ToString()</c> in the message on failure, or on a default-state Result.
    /// </summary>
    public T ValueOrThrow()
    {
        ThrowIfUninitialized();
        if (_state == StateSuccess)
        {
            return _value!;
        }

        throw new InvalidOperationException($"Result was a failure: {_error}");
    }

    /// <summary>
    /// Returns the success value. On failure, throws the exception produced by
    /// <paramref name="exceptionMapper"/>. If the mapper returns null, throws
    /// <see cref="InvalidOperationException"/> instead of letting <c>throw null</c> surface.
    /// </summary>
    public T ValueOrThrow(Func<TError, Exception> exceptionMapper)
    {
        ArgumentNullException.ThrowIfNull(exceptionMapper);
        ThrowIfUninitialized();

        if (_state == StateSuccess)
        {
            return _value!;
        }

        var ex = exceptionMapper(_error!);
        if (ex is null)
        {
            throw new InvalidOperationException("Mapped exception was null.");
        }

        throw ex;
    }

    /// <summary>Transforms the success value; passes the failure through unchanged.</summary>
    public Result<TResult, TError> Map<TResult>(Func<T, TResult> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        ThrowIfUninitialized();
        return _state == StateSuccess
            ? Result<TResult, TError>.Success(map(_value!))
            : Result<TResult, TError>.Failure(_error!);
    }

    /// <summary>Transforms the failure value; passes the success through unchanged.</summary>
    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> map) where TNewError : notnull
    {
        ArgumentNullException.ThrowIfNull(map);
        ThrowIfUninitialized();
        return _state == StateSuccess
            ? Result<T, TNewError>.Success(_value!)
            : Result<T, TNewError>.Failure(map(_error!));
    }

    /// <summary>Chains a Result-returning step; failure short-circuits.</summary>
    public Result<TResult, TError> Bind<TResult>(Func<T, Result<TResult, TError>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ThrowIfUninitialized();
        return _state == StateSuccess
            ? next(_value!)
            : Result<TResult, TError>.Failure(_error!);
    }

    /// <summary>Chains a void-Result-returning step; failure short-circuits.</summary>
    public Result<TError> Bind(Func<T, Result<TError>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ThrowIfUninitialized();
        return _state == StateSuccess
            ? next(_value!)
            : Result<TError>.Failure(_error!);
    }

    private void ThrowIfUninitialized()
    {
        if (_state == StateUninitialized)
        {
            throw new InvalidOperationException("Result is uninitialized.");
        }
    }
}
