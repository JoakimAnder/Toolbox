using System.Diagnostics.CodeAnalysis;

namespace JoakimAnder.Toolbox.Results;

/// <summary>
/// A void-success / typed-failure container. Either succeeded with no payload, or failed
/// with a <typeparamref name="TError"/>. The default value is "uninitialized" and throws
/// on every operation except <see cref="IsSuccess"/>, <see cref="IsFailure"/>, and
/// <see cref="IsDefault"/>.
/// </summary>
public readonly struct Result<TError> where TError : notnull
{
    private const byte StateUninitialized = 0;
    private const byte StateSuccess = 1;
    private const byte StateFailure = 2;

    private readonly byte _state;
    private readonly TError? _error;

    // Sentinel used to select the success constructor without a byte-state parameter.
    private readonly struct SuccessTag { }

    private Result(SuccessTag _)
    {
        _state = StateSuccess;
        _error = default;
    }

    private Result(TError error)
    {
        _state = StateFailure;
        _error = error;
    }

#pragma warning disable CA1000 // Do not declare static members on generic types — intentional factory API
    /// <summary>Creates a success result with no payload.</summary>
    public static Result<TError> Success() => new(default(SuccessTag));

    /// <summary>Creates a failure result holding <paramref name="error"/>.</summary>
    public static Result<TError> Failure(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TError>(error);
    }
#pragma warning restore CA1000

    public static implicit operator Result<TError>(TError error) => Failure(error);
    public static implicit operator Result<TError>(Failure<TError> failure) => new Result<TError>(failure.Error);

    /// <summary>True iff the result is a success.</summary>
    public bool IsSuccess => _state == StateSuccess;

    /// <summary>True iff the result is a failure.</summary>
    public bool IsFailure => _state == StateFailure;

    /// <summary>True iff the result is the default value (never constructed).</summary>
    public bool IsDefault => _state == StateUninitialized;

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
    public void Match(Action onSuccess, Action<TError> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ThrowIfUninitialized();

        if (_state == StateSuccess)
        {
            onSuccess();
        }
        else
        {
            onFailure(_error!);
        }
    }

    /// <summary>Returns the result of the matching delegate. Throws on default.</summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<TError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ThrowIfUninitialized();

        return _state == StateSuccess ? onSuccess() : onFailure(_error!);
    }

    /// <summary>
    /// Returns silently on success; throws <see cref="InvalidOperationException"/> with the
    /// error's <c>ToString()</c> in the message on failure, or on a default-state Result.
    /// </summary>
    public void ThrowIfFailure()
    {
        ThrowIfUninitialized();
        if (_state == StateFailure)
        {
            throw new InvalidOperationException($"Result was a failure: {_error}");
        }
    }

    /// <summary>
    /// Returns silently on success. On failure, throws the exception produced by
    /// <paramref name="exceptionMapper"/>. If the mapper returns null, throws
    /// <see cref="InvalidOperationException"/> instead of <c>throw null</c>.
    /// </summary>
    public void ThrowIfFailure(Func<TError, Exception> exceptionMapper)
    {
        ArgumentNullException.ThrowIfNull(exceptionMapper);
        ThrowIfUninitialized();

        if (_state != StateFailure)
        {
            return;
        }

        var ex = exceptionMapper(_error!);
        if (ex is null)
        {
            throw new InvalidOperationException("Mapped exception was null.");
        }

        throw ex;
    }

    /// <summary>Transforms the failure value; passes the success through unchanged.</summary>
    public Result<TNewError> MapError<TNewError>(Func<TError, TNewError> map) where TNewError : notnull
    {
        ArgumentNullException.ThrowIfNull(map);
        ThrowIfUninitialized();
        return _state == StateSuccess
            ? Result<TNewError>.Success()
            : Result<TNewError>.Failure(map(_error!));
    }

    /// <summary>Chains a void-Result-returning step; failure short-circuits.</summary>
    public Result<TError> Bind(Func<Result<TError>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ThrowIfUninitialized();
        return _state == StateSuccess ? next() : this;
    }

    /// <summary>Chains a typed-Result-returning step; failure short-circuits.</summary>
    public Result<T, TError> Bind<T>(Func<Result<T, TError>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ThrowIfUninitialized();
        return _state == StateSuccess
            ? next()
            : Result<T, TError>.Failure(_error!);
    }

    private void ThrowIfUninitialized()
    {
        if (_state == StateUninitialized)
        {
            throw new InvalidOperationException("Result is uninitialized.");
        }
    }
}
