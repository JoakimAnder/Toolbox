namespace JoakimAnder.Toolbox.Results;

/// <summary>
/// Static helpers for constructing <see cref="Result{T, TError}"/> and <see cref="Result{TError}"/>.
/// </summary>
public static partial class Result
{
    /// <summary>
    /// Returns an inferred-type success carrier that implicitly converts to
    /// <see cref="Result{T, TError}"/> for any compatible <typeparamref name="TError"/>.
    /// Use this when the bare implicit conversion from <typeparamref name="T"/> is ambiguous
    /// (e.g., when <typeparamref name="T"/> and the target's <c>TError</c> are the same type).
    /// </summary>
    public static Success<T> Success<T>(T value) => new(value);

    /// <summary>
    /// Returns an inferred-type failure carrier that implicitly converts to
    /// <see cref="Result{T, TError}"/> or <see cref="Result{TError}"/>.
    /// Use this when the bare implicit conversion from <typeparamref name="TError"/> is ambiguous.
    /// </summary>
    public static Failure<TError> Failure<TError>(TError error) where TError : notnull
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Failure<TError>(error);
    }
}
